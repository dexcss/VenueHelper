using VenueHelper.Data;

namespace VenueHelper.Logic;

// Manages saved bar-game definitions and derives human-readable rules + the
// command players should roll + the payout.
public class BarGameService
{
    private readonly Plugin Plugin;
    private Configuration Config => Plugin.Configuration;

    public BarGameService(Plugin plugin) => Plugin = plugin;

    public List<BarGame> Games => Config.BarGames;

    public BarGame AddGame(string name)
    {
        var g = new BarGame(string.IsNullOrWhiteSpace(name) ? "New Game" : name.Trim());
        Config.BarGames.Add(g);
        Config.SelectedBarGame = Config.BarGames.Count - 1;
        Config.Save();
        return g;
    }

    public void RemoveGame(BarGame g)
    {
        Config.BarGames.Remove(g);
        if (Config.SelectedBarGame >= Config.BarGames.Count)
            Config.SelectedBarGame = Math.Max(0, Config.BarGames.Count - 1);
        Config.Save();
    }

    // ---- Derived helpers ----

    public static string RollCommand(BarGame g) => g.Roll switch
    {
        RollKind.RandomPlain => "/random",
        RollKind.RandomOutOf => $"/random {g.RollCeiling}",
        RollKind.Dice => $"/dice {g.RollCeiling}",
        _ => "/random",
    };

    public static string WinDescription(BarGame g) => g.Condition switch
    {
        WinCondition.SpecificNumbers => g.WinningNumbers.Count == 0
            ? "land on the winning number"
            : $"land on {string.Join(" or ", g.WinningNumbers)}",
        WinCondition.InRange => $"land between {g.RangeLow} and {g.RangeHigh}",
        WinCondition.Highest => "roll the highest",
        WinCondition.Lowest => "roll the lowest",
        WinCondition.ClosestTo => $"land closest to {g.ClosestTarget}",
        _ => "win",
    };

    public static string PrizeDescription(BarGame g) => g.Prize switch
    {
        PrizeKind.FixedGil => $"{g.PrizeGil:N0} gil",
        PrizeKind.PercentOfPot => $"{g.PrizePercent:0}% of the pot",
        _ => "a prize",
    };

    // The payout for a win right now, given the live pot.
    public static long Payout(BarGame g) => g.Prize switch
    {
        PrizeKind.FixedGil => g.PrizeGil,
        PrizeKind.PercentOfPot => (long)(g.CurrentPot * (g.PrizePercent / 100f)),
        _ => 0,
    };

    // A one-line rules summary for announcing.
    public static string RulesLine(BarGame g)
    {
        var cmd = RollCommand(g);
        var win = WinDescription(g);
        var cost = g.EntryCost > 0 ? $" ({g.EntryCost:N0} gil to play)" : "";

        // Announce the actual gil amount the winner gets. For a percent-of-pot
        // prize this is the live pot payout; the wording notes it's the pot.
        string prize;
        if (g.Prize == PrizeKind.PercentOfPot)
        {
            var payout = Payout(g);
            prize = g.PrizePercent >= 100f
                ? $"the whole pot ({payout:N0} gil)"
                : $"{payout:N0} gil ({g.PrizePercent:0}% of the pot)";
        }
        else
        {
            prize = $"{g.PrizeGil:N0} gil";
        }

        return $"{g.Name}: {cmd} and {win} to win {prize}!{cost}";
    }

    // Check whether a roll value wins (for SpecificNumbers / InRange only;
    // Highest/Lowest/ClosestTo are comparative and judged across players).
    public static bool IsWinningRoll(BarGame g, int roll) => g.Condition switch
    {
        WinCondition.SpecificNumbers => g.WinningNumbers.Contains(roll),
        WinCondition.InRange => roll >= g.RangeLow && roll <= g.RangeHigh,
        _ => false,
    };

    // Pot management.
    public void ResetPot(BarGame g) { g.CurrentPot = g.StartingPot; g.PotStarted = true; Config.Save(); }
    public void AdjustPot(BarGame g, long delta) { g.CurrentPot = Math.Max(0, g.CurrentPot + delta); g.PotStarted = true; Config.Save(); }

    // ---- Live play tracking --------------------------------------------

    public BarGame? ActiveTrackingGame => Config.BarGames.FirstOrDefault(g => g.Tracking);

    public void StartTracking(BarGame g)
    {
        foreach (var other in Config.BarGames) other.Tracking = false;
        g.Tracking = true;
        Config.Save();
    }

    public void StopTracking(BarGame g) { g.Tracking = false; Config.Save(); }

    public void ClearPlays(BarGame g)
    {
        g.Players.Clear();
        g.Plays.Clear();
        Config.Save();
    }

    private BarGamePlayer ResolvePlayer(BarGame g, string fullName)
    {
        if (g.Players.TryGetValue(fullName, out var p))
            return p;
        var bare = StripWorldB(fullName);
        var existing = g.Players.Values.FirstOrDefault(x => StripWorldB(x.FullName) == bare);
        if (existing != null) return existing;
        var np = new BarGamePlayer(fullName);
        g.Players[fullName] = np;
        return np;
    }

    // Trade watcher calls this: credit gil toward the active game's buy-ins.
    // Paid buy-ins also grow a stacking pot.
    public void CreditTrade(string fullName, long gil)
    {
        var g = ActiveTrackingGame;
        if (g == null || gil <= 0) return;
        var p = ResolvePlayer(g, fullName);
        var before = p.PlaysBought(g.EntryCost);
        p.GilPaid += gil;
        var after = p.PlaysBought(g.EntryCost);
        // Grow the stacking pot by the number of new plays this trade bought.
        GrowPot(g, after - before);
        Config.Save();
    }

    // Manually add ONE paid play (e.g. paid another way). Grows the pot.
    public void AddManualPlay(BarGame g, string fullName)
    {
        var p = ResolvePlayer(g, fullName);
        p.GilPaid += Math.Max(1, g.EntryCost);
        GrowPot(g, 1);
        Config.Save();
    }

    // Add a FREE play: the player gets a roll but it does NOT add to the pot.
    public void AddFreebie(BarGame g, string fullName)
    {
        var p = ResolvePlayer(g, fullName);
        // Grant a play's worth of "paid" allowance so a roll is accepted, but
        // don't grow the pot.
        p.GilPaid += Math.Max(1, g.EntryCost);
        Config.Save();
    }

    // Grow a stacking pot by n plays' worth (the entry cost each). No-op if not
    // stacking.
    private void GrowPot(BarGame g, int plays)
    {
        if (!g.StackingPot || plays <= 0) return;
        if (!g.PotStarted) { g.CurrentPot = g.StartingPot; g.PotStarted = true; }
        g.CurrentPot += g.EntryCost * plays;
    }

    // Hook calls this on every /random or /dice while a game is tracking.
    public void OnRoll(string fullName, int result, int outOf)
    {
        var g = ActiveTrackingGame;
        if (g == null) return;

        var normalized = outOf <= 0 ? (g.Roll == RollKind.RandomPlain ? 1000 : g.RollCeiling) : outOf;

        // If the game expects a specific ceiling, ignore mismatched rolls.
        if (g.Roll != RollKind.RandomPlain && g.RollCeiling > 0 && normalized != g.RollCeiling)
            return;

        if (g.EntryCost > 0)
        {
            var p = ResolvePlayer(g, fullName);
            if (p.PlaysRemaining(g.EntryCost) <= 0)
            {
                Plugin.Log.Information($"Bar game: ignored roll from {p.NameOnly} (no paid play).");
                return;
            }
            p.PlaysUsed += 1;
        }
        else
        {
            ResolvePlayer(g, fullName);
        }

        var won = IsWinningRoll(g, result);
        g.Plays.Add(new BarGamePlay(fullName, result, normalized, won));
        Config.Save();
    }

    public static BarGamePlay? ComparativeWinner(BarGame g)
    {
        if (g.Plays.Count == 0) return null;
        return g.Condition switch
        {
            WinCondition.Highest => g.Plays.OrderByDescending(p => p.Roll).First(),
            WinCondition.Lowest => g.Plays.OrderBy(p => p.Roll).First(),
            WinCondition.ClosestTo => g.Plays.OrderBy(p => Math.Abs(p.Roll - g.ClosestTarget)).First(),
            _ => null,
        };
    }

    public static bool IsComparative(BarGame g) =>
        g.Condition is WinCondition.Highest or WinCondition.Lowest or WinCondition.ClosestTo;

    private static string StripWorldB(string full)
    {
        var idx = full.IndexOf('\uE05D');
        return idx < 0 ? full : full[..idx];
    }
}
