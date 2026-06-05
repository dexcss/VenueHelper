using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;

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

    public VenueCounter(Plugin plugin)
    {
        Plugin = plugin;
    }

    public bool AllNightRunning => Config.AllNightRunning;
    public int AllNightTotal => Config.AllNightSeen.Count;
    public DateTime AllNightStarted => Config.AllNightStarted;
    public int TempTotal => TempRunning ? TempSeen.Count : LastLapTotal;

    // The unique visitors gathered tonight (Name\uE05DWorld keys), for export.
    public IReadOnlyCollection<string> AllNightVisitors => Config.AllNightSeen;

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
        Config.AllNightSeen.Clear();
        Config.AllNightStarted = DateTime.Now;
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
        Config.Save();
    }

    public void ResetAllNight()
    {
        Config.AllNightSeen.Clear();
        Config.AllNightStarted = DateTime.Now;
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
        lastScan = DateTime.Now;

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
                if (Config.AllNightSeen.Add(key))
                    allNightDirty = true;
            }
        }

        CurrentlyVisible = visible;

        // Persist the all-night set when it grew, so a crash/relog doesn't lose
        // the night's tally. Save only on change to avoid disk churn each second.
        if (allNightDirty)
            Config.Save();
    }
}
