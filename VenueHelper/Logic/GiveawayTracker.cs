using VenueHelper.Data;

namespace VenueHelper.Logic;

// Tracks a giveaway round. While running, captures each player's FIRST /random
// after Start (later rolls by the same player are ignored for winner selection
// but still appear in the verification feed). Computes highest / lowest /
// closest-to-target winners.
//
// All state lives in the plugin Configuration and is saved on every change, so
// a game crash mid-giveaway loses nothing - the rolls, the running state, and
// the mode selection all come back on reload. State is cleared only when the
// host hits Reset, or Start (which begins a fresh round).
public class GiveawayTracker
{
    private readonly Plugin Plugin;
    private Configuration Config => Plugin.Configuration;

    // Fast lookup of which players already have a counted (first) roll. Rebuilt
    // from the persisted ordered list on construction.
    private readonly HashSet<string> counted = new();
    private const int MaxFeed = 200;

    public GiveawayTracker(Plugin plugin)
    {
        Plugin = plugin;
        foreach (var r in Config.GiveawayOrdered)
            counted.Add(r.FullName);
    }

    public bool Running => Config.GiveawayRunning;
    public DateTime StartedAt => Config.GiveawayStarted;

    public IReadOnlyList<GiveawayRoll> Entries => Config.GiveawayOrdered;
    public IReadOnlyList<GiveawayRoll> Feed => Config.GiveawayFeed;
    public int Count => Config.GiveawayOrdered.Count;

    public GiveawayMode Modes
    {
        get => (GiveawayMode)Config.GiveawayModes;
        set { Config.GiveawayModes = (int)value; Config.Save(); }
    }

    public int ClosestTarget
    {
        get => Config.GiveawayClosestTarget;
        set { Config.GiveawayClosestTarget = Math.Max(0, value); Config.Save(); }
    }

    // Race mode: the exact number players are rolling for.
    public int MatchTarget
    {
        get => Config.GiveawayMatchTarget;
        set { Config.GiveawayMatchTarget = Math.Max(0, value); Config.Save(); }
    }

    public bool ExactMatchOn => Modes.HasFlag(GiveawayMode.ExactMatch);

    // The winning roll of an exact-match race, if one has been hit.
    public GiveawayRoll? MatchWinner =>
        string.IsNullOrEmpty(Config.GiveawayMatchWinnerId)
            ? null
            : Config.GiveawayFeed.FirstOrDefault(r => r.Id.ToString() == Config.GiveawayMatchWinnerId);

    public void Start()
    {
        counted.Clear();
        Config.GiveawayOrdered.Clear();
        Config.GiveawayFeed.Clear();
        Config.GiveawayMatchWinnerId = string.Empty;
        Config.GiveawayStarted = DateTime.Now;
        Config.GiveawayRunning = true;
        Config.Save();
    }

    public void Stop()
    {
        Config.GiveawayRunning = false;
        Config.Save();
    }

    public void Reset()
    {
        counted.Clear();
        Config.GiveawayOrdered.Clear();
        Config.GiveawayFeed.Clear();
        Config.GiveawayMatchWinnerId = string.Empty;
        Config.GiveawayRunning = false;
        Config.Save();
    }

    // Records a rejected roll in the feed (flagged invalid) so the host sees it.
    private void LogInvalid(string fullName, int roll, int outOf, string reason)
    {
        var bad = new GiveawayRoll
        {
            FullName = fullName,
            Roll = roll,
            OutOf = outOf,
            When = DateTime.Now,
            Invalid = true,
            InvalidReason = reason,
        };
        Config.GiveawayFeed.Insert(0, bad);
        if (Config.GiveawayFeed.Count > MaxFeed)
            Config.GiveawayFeed.RemoveRange(MaxFeed, Config.GiveawayFeed.Count - MaxFeed);
        Config.Save();
    }

    // Called by the hook for every /random or /dice while a giveaway is running.
    public void OnRoll(string fullName, int roll, int outOf, bool isDice = false)
    {
        if (!Config.GiveawayRunning)
            return;

        // /dice only counts if the host enabled it; default is /random only.
        if (isDice && !Config.GiveawayAllowDice)
        {
            LogInvalid(fullName, roll, outOf, "used /dice \u2014 /dice is not enabled for this giveaway");
            return;
        }

        // Enforce plain /random for /random rolls: a /random N (outOf > 0 and not
        // the default 999) is rejected so players can't shrink the range. Still
        // logged in the feed, flagged invalid, so the host can see it.
        var isPlain = outOf <= 0 || outOf == 999;
        if (!isDice && Config.GiveawayPlainRandomOnly && !isPlain)
        {
            LogInvalid(fullName, roll, outOf, $"used /random {outOf} \u2014 must use plain /random");
            return;
        }

        var entry = new GiveawayRoll
        {
            FullName = fullName,
            Roll = roll,
            OutOf = outOf <= 0 ? 999 : outOf,
            When = DateTime.Now,
        };

        // Always record in the feed for verification.
        Config.GiveawayFeed.Insert(0, entry);
        if (Config.GiveawayFeed.Count > MaxFeed)
            Config.GiveawayFeed.RemoveRange(MaxFeed, Config.GiveawayFeed.Count - MaxFeed);

        if (ExactMatchOn)
        {
            // Race mode: every roll counts (a player may roll many times). The
            // first roll equal to the target wins and stops the race.
            Config.GiveawayOrdered.Add(entry);
            if (string.IsNullOrEmpty(Config.GiveawayMatchWinnerId) && roll == Config.GiveawayMatchTarget)
            {
                Config.GiveawayMatchWinnerId = entry.Id.ToString();
                Config.GiveawayRunning = false; // auto-stop on a winning hit
            }
        }
        else
        {
            // Standard mode: only the first roll per player counts.
            if (counted.Add(fullName))
                Config.GiveawayOrdered.Add(entry);
        }

        // Persist after each roll so a crash can't lose it.
        Config.Save();
    }

    // ---- Winners -------------------------------------------------------

    public GiveawayRoll? Highest => Count == 0 ? null : Config.GiveawayOrdered.MaxBy(e => e.Roll);
    public GiveawayRoll? Lowest => Count == 0 ? null : Config.GiveawayOrdered.MinBy(e => e.Roll);

    public GiveawayRoll? Closest
    {
        get
        {
            if (Count == 0) return null;
            return Config.GiveawayOrdered.MinBy(e => Math.Abs(e.Roll - ClosestTarget));
        }
    }

    // True if this roll is a winner under any currently-active mode.
    public bool IsWinner(GiveawayRoll roll)
    {
        if (ExactMatchOn)
            return roll.Id.ToString() == Config.GiveawayMatchWinnerId;
        if (Modes.HasFlag(GiveawayMode.Highest) && roll.Id == Highest?.Id) return true;
        if (Modes.HasFlag(GiveawayMode.Lowest) && roll.Id == Lowest?.Id) return true;
        if (Modes.HasFlag(GiveawayMode.Closest) && roll.Id == Closest?.Id) return true;
        return false;
    }

    // Whether a feed roll is the counted (first) roll for its player.
    public bool IsCounted(GiveawayRoll feedRoll) =>
        Config.GiveawayOrdered.Any(e => e.Id == feedRoll.Id);
}
