using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using VenueHelper.Data;

namespace VenueHelper.Logic;

// Counts unique players seen around the host. FFXIV only renders ~99 characters
// at once, so a single snapshot undercounts a busy venue. The fix is to keep a
// running set of names: as the host walks the venue, players stream in and out
// of render range, and every new name gets added to the set permanently.
//
// Two independent sets run off the same object-table scan:
//   * Temporary ("lap") set: cleared on Start, frozen on Stop. Use it to do one
//     sweep of the room and get a clean headcount.
//   * All-night set: persists across the whole night (and across sessions, via
//     config) until the host resets it, for a grand total of unique visitors.
// Both can run at the same time off one scan per tick.
public unsafe class VenueCounter
{
    private readonly Plugin Plugin;
    private Configuration Config => Plugin.Configuration;
    private VenueProfile Venue => Config.ActiveVenueProfile;

    // ---- Venue profiles ------------------------------------------------

    public IReadOnlyList<VenueProfile> Venues => Config.Venues;
    public int ActiveVenueIndex => Config.ActiveVenue;
    public string ActiveVenueName => Venue.Name;

    public void AddVenue(string name)
    {
        Config.Venues.Add(new VenueProfile(string.IsNullOrWhiteSpace(name) ? "New Venue" : name.Trim()));
        Config.ActiveVenue = Config.Venues.Count - 1;
        Config.Save();
    }

    public void SwitchVenue(int index)
    {
        if (index < 0 || index >= Config.Venues.Count) return;
        // Close any open sessions on the venue we're leaving so its data is tidy.
        CloseAllOpenSessions();
        Config.ActiveVenue = index;
        Config.Save();
    }

    public void RenameVenue(VenueProfile v, string name)
    {
        v.Name = string.IsNullOrWhiteSpace(name) ? v.Name : name.Trim();
        Config.Save();
    }

    public void RemoveVenue(VenueProfile v)
    {
        // Never leave zero venues.
        if (Config.Venues.Count <= 1) return;
        var idx = Config.Venues.IndexOf(v);
        Config.Venues.Remove(v);
        if (Config.ActiveVenue >= Config.Venues.Count)
            Config.ActiveVenue = Config.Venues.Count - 1;
        else if (idx <= Config.ActiveVenue && Config.ActiveVenue > 0)
            Config.ActiveVenue--;
        Config.Save();
    }

    // Temporary lap counter state (not persisted - it's meant to be short-lived).
    public bool TempRunning { get; private set; }
    public readonly HashSet<string> TempSeen = new();
    public DateTime TempStarted { get; private set; }
    // Frozen total from the last completed lap (shown after Stop).
    public int LastLapTotal { get; private set; }

    // How many players were visible (rendered) on the most recent scan.
    public int CurrentlyVisible { get; private set; }

    // Throttle the scan to once per second; the object table doesn't change
    // faster than players can walk in, and a per-frame scan is wasteful.
    private DateTime lastScan = DateTime.MinValue;
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMilliseconds(1000);

    // Visit-session tracking: last time each player was actually seen, used to
    // decide when an open session should be closed. Players flicker out of
    // render range, so a departure only counts after a grace period.
    private readonly Dictionary<string, DateTime> lastSeenAt = new();
    private static readonly TimeSpan DepartureGrace = TimeSpan.FromMinutes(30);

    public VenueCounter(Plugin plugin)
    {
        Plugin = plugin;
    }

    public bool AllNightRunning => Config.AllNightRunning;
    public int AllNightTotal => Venue.AllNightSeen.Count;
    public DateTime AllNightStarted => Venue.AllNightStarted;
    public int TempTotal => TempRunning ? TempSeen.Count : LastLapTotal;

    // The unique visitors gathered tonight (Name\uE05DWorld keys), for export.
    public IReadOnlyCollection<string> AllNightVisitors => Venue.AllNightSeen;

    // ---- Lifetime time tracking ----------------------------------------

    public bool TrackVisitTime
    {
        get => Config.TrackVisitTime;
        set { Config.TrackVisitTime = value; Config.Save(); }
    }

    // Players and their total seconds, highest first.
    public IEnumerable<(string Key, long Seconds)> VisitTimes =>
        Venue.VisitSeconds.Select(kv => (Key: kv.Key, Seconds: kv.Value)).OrderByDescending(x => x.Seconds);

    public void ResetVisitTimes()
    {
        Venue.VisitSeconds.Clear();
        Venue.VisitSessions.Clear();
        lastSeenAt.Clear();
        Config.Save();
    }

    public void RemoveVisitTime(string key)
    {
        Venue.VisitSeconds.Remove(key);
        Venue.VisitSessions.Remove(key);
        lastSeenAt.Remove(key);
        Config.Save();
    }

    // Visit sessions for one player, most recent first.
    public IReadOnlyList<VisitSession> SessionsFor(string key) =>
        Venue.VisitSessions.TryGetValue(key, out var s)
            ? s.OrderByDescending(x => x.Arrived).ToList()
            : new List<VisitSession>();

    // "21H 31M" style. Seconds shown only under a minute.
    public static string FormatDuration(long seconds)
    {
        if (seconds < 60) return $"{seconds}s";
        var h = seconds / 3600;
        var m = (seconds % 3600) / 60;
        if (h > 0) return $"{h}H {m}M";
        return $"{m}M";
    }

    public static string NameOnly(string key)
    {
        var idx = key.IndexOf('\uE05D');
        return idx < 0 ? key : key[..idx];
    }

    public static string WorldOf(string key)
    {
        var idx = key.IndexOf('\uE05D');
        return idx < 0 ? string.Empty : key[(idx + 1)..];
    }

    // ---- Temporary lap counter -----------------------------------------

    public void StartTemp()
    {
        TempSeen.Clear();
        TempStarted = DateTime.Now;
        TempRunning = true;
    }

    public void StopTemp()
    {
        LastLapTotal = TempSeen.Count;
        TempRunning = false;
    }

    public void ClearTemp()
    {
        TempSeen.Clear();
        LastLapTotal = 0;
        TempRunning = false;
    }

    // ---- All night counter ---------------------------------------------

    public void StartAllNight()
    {
        // Starting fresh resets the set and the start time.
        Venue.AllNightSeen.Clear();
        Venue.AllNightStarted = DateTime.Now;
        Config.AllNightRunning = true;
        Config.Save();
    }

    public void ResumeAllNight()
    {
        // Keep the existing set; just turn tracking back on.
        Config.AllNightRunning = true;
        Config.Save();
    }

    public void StopAllNight()
    {
        Config.AllNightRunning = false;
        CloseAllOpenSessions();
        Config.Save();
    }

    // Close any still-open visit sessions (e.g. when tracking stops) at their
    // last-seen time, or now if never recorded.
    private void CloseAllOpenSessions()
    {
        var now = DateTime.Now;
        foreach (var kv in Venue.VisitSessions)
        {
            foreach (var s in kv.Value.Where(s => s.Open))
            {
                s.Left = lastSeenAt.TryGetValue(kv.Key, out var seen) ? seen : now;
                s.Open = false;
            }
        }
        lastSeenAt.Clear();
    }

    public void ResetAllNight()
    {
        Venue.AllNightSeen.Clear();
        Venue.AllNightStarted = DateTime.Now;
        Config.AllNightRunning = false;
        Config.Save();
    }

    // ---- Scan ----------------------------------------------------------

    // Called every framework tick from Plugin. Does one throttled object-table
    // sweep and feeds whichever counters are active.
    public void Update()
    {
        if (!TempRunning && !Config.AllNightRunning)
            return;

        if (DateTime.Now - lastScan < ScanInterval)
            return;
        // Real elapsed seconds since the last scan, credited to each visible
        // player for lifetime time tracking. Clamp so a long pause (alt-tab,
        // sleep) doesn't dump a huge chunk onto everyone currently visible.
        var elapsed = lastScan == DateTime.MinValue ? 0 : (DateTime.Now - lastScan).TotalSeconds;
        if (elapsed > 5) elapsed = 1; // treat gaps as a single interval
        lastScan = DateTime.Now;

        var trackTime = Config.AllNightRunning && Config.TrackVisitTime && elapsed > 0;

        var visible = 0;
        var allNightDirty = false;

        foreach (var o in Plugin.Objects)
        {
            if (o is not IPlayerCharacter pc)
                continue;

            // Skip nameless objects (portraits / adventurer plates show up blank).
            if (pc.Name.TextValue.Length == 0)
                continue;
            // SubKind 4 is a normal player character (matches VenueManager's filter).
            if (o.SubKind != 4)
                continue;

            visible++;

            // Key by Name@World so two players with the same name on different
            // worlds aren't merged.
            var key = pc.HomeWorld.ValueNullable != null
                ? $"{pc.Name.TextValue}\uE05D{pc.HomeWorld.Value.Name}"
                : pc.Name.TextValue;

            if (TempRunning)
                TempSeen.Add(key);

            if (Config.AllNightRunning)
            {
                if (Venue.AllNightSeen.Add(key))
                    allNightDirty = true;
            }

            // Lifetime time: credit this player the elapsed interval.
            if (trackTime)
            {
                Venue.VisitSeconds.TryGetValue(key, out var secs);
                Venue.VisitSeconds[key] = secs + (long)Math.Round(elapsed);
                allNightDirty = true;

                // Visit sessions: open one if this player has no currently-open
                // session, otherwise just refresh their last-seen time.
                lastSeenAt[key] = DateTime.Now;
                if (!Venue.VisitSessions.TryGetValue(key, out var sessions))
                {
                    sessions = new List<VisitSession>();
                    Venue.VisitSessions[key] = sessions;
                }
                var open = sessions.LastOrDefault(s => s.Open);
                if (open == null)
                    sessions.Add(new VisitSession(DateTime.Now));
                else
                    open.LastSeen = DateTime.Now;
            }
        }

        CurrentlyVisible = visible;

        // Close visit sessions for players not seen within the grace period
        // (set their Left time to when they were last actually seen).
        if (Config.AllNightRunning && Config.TrackVisitTime)
        {
            var now = DateTime.Now;
            foreach (var kv in Venue.VisitSessions)
            {
                var open = kv.Value.LastOrDefault(s => s.Open);
                if (open == null) continue;
                if (lastSeenAt.TryGetValue(kv.Key, out var seen))
                {
                    if (now - seen > DepartureGrace)
                    {
                        open.Left = seen;     // they left when last actually seen
                        open.Open = false;
                        allNightDirty = true;
                    }
                }
                else
                {
                    // No record of being seen this session run; close it now.
                    open.Open = false;
                    allNightDirty = true;
                }
            }
        }

        // Persist the all-night set when it grew, so a crash/relog doesn't lose
        // the night's tally. Save only on change to avoid disk churn each second.
        if (allNightDirty)
            Config.Save();
    }
}
