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
    // Accumulated visit-seconds are flushed to disk no more often than this,
    // instead of every scan (which serialized the whole config each second).
    private static readonly TimeSpan TimeFlushInterval = TimeSpan.FromSeconds(30);
    private DateTime lastTimeFlush = DateTime.MinValue;
    // Per-scan ContentId caches (transient, not persisted) to avoid rebuilding
    // the Name@World string and rescanning sessions every second.
    private readonly HashSet<ulong> seenThisScan = new();
    private readonly Dictionary<ulong, string> keyByContentId = new();
    private readonly Dictionary<ulong, VisitSession> openSession = new();
    private DateTime lastDepartureSweep = DateTime.MinValue;
    private static readonly TimeSpan DepartureSweepInterval = TimeSpan.FromSeconds(5);

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
        openSession.Clear();
        Config.Save();
    }

    public void RemoveVisitTime(string key)
    {
        Venue.VisitSeconds.Remove(key);
        Venue.VisitSessions.Remove(key);
        lastSeenAt.Remove(key);
        openSession.Clear(); // force session re-resolution next scan (cheap)
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
        openSession.Clear();
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
        var addSeconds = (long)Math.Round(elapsed);

        var visible = 0;
        var allNightDirty = false;
        var now = DateTime.Now;

        // Reuse a single scratch set of ContentIds seen this scan (cleared, not
        // reallocated) so departure detection is a cheap hash lookup.
        seenThisScan.Clear();

        foreach (var o in Plugin.Objects)
        {
            // Filter to real player characters cheaply BEFORE touching strings.
            if (o is not IPlayerCharacter pc)
                continue;
            if (o.SubKind != 4) // 4 = normal player character
                continue;

            // ContentId is the game's stable unique identity for a player. Use it
            // as the fast key for all per-scan work; only build the human-readable
            // "Name@World" string the first time we see this player (then cache).
            var cid = pc.GameObjectId;
            if (cid == 0) continue;

            visible++;
            seenThisScan.Add(cid);

            // Resolve (and cache) the string key for this ContentId. Building it
            // is the expensive part (name + world resolution + concat), so we do
            // it once per player rather than every scan.
            if (!keyByContentId.TryGetValue(cid, out var key))
            {
                var name = pc.Name.TextValue;
                if (name.Length == 0) continue; // nameless portrait/plate
                key = pc.HomeWorld.ValueNullable != null
                    ? $"{name}\uE05D{pc.HomeWorld.Value.Name}"
                    : name;
                keyByContentId[cid] = key;
            }

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
                Venue.VisitSeconds[key] = secs + addSeconds;
                lastSeenAt[key] = now;

                // Open-session lookup is cached per ContentId to avoid the
                // LastOrDefault scan every second.
                if (!openSession.TryGetValue(cid, out var open) || open == null || !open.Open)
                {
                    if (!Venue.VisitSessions.TryGetValue(key, out var sessions))
                    {
                        sessions = new List<VisitSession>();
                        Venue.VisitSessions[key] = sessions;
                    }
                    open = sessions.LastOrDefault(s => s.Open);
                    if (open == null)
                    {
                        open = new VisitSession(now);
                        sessions.Add(open);
                        allNightDirty = true; // a new session is worth persisting
                    }
                    openSession[cid] = open;
                }
                else
                {
                    open.LastSeen = now;
                }
            }
        }

        CurrentlyVisible = visible;

        // Close visit sessions for players not seen within the grace period.
        // This is a 30-min grace, so checking every scan is wasteful \u2014 sweep at
        // most once every few seconds.
        if (Config.AllNightRunning && Config.TrackVisitTime && now - lastDepartureSweep >= DepartureSweepInterval)
        {
            lastDepartureSweep = now;
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

        // Persist on a meaningful change (new player or session open/close)
        // immediately. Accumulated seconds are flushed on a slow cadence so we
        // don't serialize the whole config every scan (the lag culprit).
        if (allNightDirty)
        {
            Config.Save();
            lastTimeFlush = DateTime.Now;
        }
        else if (trackTime && DateTime.Now - lastTimeFlush >= TimeFlushInterval)
        {
            Config.Save();
            lastTimeFlush = DateTime.Now;
        }
    }
}
