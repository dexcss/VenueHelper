using Dalamud.Configuration;
using Dalamud.Plugin;
using VenueHelper.Data;

namespace VenueHelper;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // ---- Exports -------------------------------------------------------
    // Folder where CSV/TXT exports are written. Empty = fall back to the
    // plugin config directory. The host can set this to anywhere they like.
    public string ExportDirectory = string.Empty;

    // ---- Venue Counter -------------------------------------------------
    // The "all night" counter persists across sessions until the host resets it.
    // Names are stored so re-entries during the night aren't double counted.
    // NOTE: these legacy single-venue fields are kept only for one-time
    // migration into Venues[] below; the active venue is the source of truth.
    public HashSet<string> AllNightSeen = new();
    public DateTime AllNightStarted = DateTime.Now;
    public bool AllNightRunning = false;
    public Dictionary<string, long> VisitSeconds = new();
    public Dictionary<string, List<VisitSession>> VisitSessions = new();
    // Whether to accumulate lifetime time while the all-night counter runs.
    public bool TrackVisitTime = true;

    // Multiple venues: each VenueProfile has its own visitor set + time DB.
    public List<VenueProfile> Venues = new();
    public int ActiveVenue = 0;
    // Set once the legacy single-venue data has been migrated into Venues[].
    public bool VenuesMigrated = false;
    public bool MenuMigrated = false;

    // ---- Raffle Helper -------------------------------------------------
    public List<RaffleEntry> RaffleEntries = new();
    // Gil cost of a single ticket (used when auto-crediting detected trades).
    public long TicketCost = 100000;
    // When a trade is detected, auto-credit it to the raffle as tickets.
    public bool RaffleAutoTrade = true;
    // Master enable for the raffle: when off, no trade tracking happens at all.
    // The host turns this on when a raffle is running.
    public bool RaffleActive = false;
    public string RaffleWinner = string.Empty; // current raffle winner (NameOnly or full)
    // House cut percent for raffles (e.g. 20 for an 80/20 split). Whole numbers.
    public float RaffleHouseCutPercent = 0f;

    // ---- Auction Helper ------------------------------------------------
    public List<AuctionEntry> AuctionEntries = new();
    public List<AuctionRecord> AuctionHistory = new();
    // Percentage the house keeps from each sale (0-100).
    public float HouseCutPercent = 0f;
    // Tracked buyers and their aliases (for "how much did X spend" across alts).
    public List<BuyerProfile> Buyers = new();

    // ---- Shout/Yell Helper ---------------------------------------------
    // Preset announcement lines (channel + message) the host can fire quickly.
    public List<ShoutPreset> ShoutPresets = new();
    // How many text lines tall each shout box is (1-4).
    public int ShoutBoxLines = 2;
    // Staff roster (persists across nights).
    public List<Employee> Employees = new();
    // Tab order + visibility. Empty = use default order with all shown. Each
    // entry is a tab id (see MainWindow.TabIds); hidden tabs are listed in
    // HiddenTabs. New tabs not present here are appended and shown by default.
    public List<string> TabOrder = new();
    public List<string> HiddenTabs = new();

    // ---- Bar Game Helper -----------------------------------------------
    public List<BarGame> BarGames = new();
    public int SelectedBarGame = 0;

    // ---- Menu Helper ---------------------------------------------------
    // Menu profiles (e.g. one per venue). Each holds its own items + macros.
    public List<MenuProfile> MenuProfiles = new();
    public int SelectedMenuProfile = 0;
    public List<MenuSale> MenuSales = new();   // sales are global for the night
    // Legacy flat fields (migrated into the first profile on load).
    public List<MenuItem> MenuItems = new();
    public bool MenuConfirmServe = false;
    public int MenuServeStepDelayMs = 1200;
    public List<QuickAction> QuickEmotes = new();
    public List<QuickAction> QuickSays = new();

    // ---- Giveaway Helper -----------------------------------------------
    // Persisted so a crash mid-giveaway doesn't lose the rolls. Cleared only
    // when the host hits Reset (or Start, which begins a fresh round).
    public List<GiveawayRoll> GiveawayOrdered = new();   // first roll per player
    public List<GiveawayRoll> GiveawayFeed = new();      // every roll, newest first
    public bool GiveawayRunning = false;
    // When on, only a plain /random (out of 999) counts; /random N is rejected
    // so players can't pick a small range to game the result.
    public bool GiveawayPlainRandomOnly = true;
    // When on, /dice rolls also count in giveaways (default: /random only).
    public bool GiveawayAllowDice = false;

    // ---- Global settings (Settings tab) --------------------------------
    // Master kill switch: when on, the plugin sends no chat and watches no
    // trades. Reversible; for when something misbehaves mid-event.
    public bool PanicMode = false;
    // When on, destructive actions require a two-stage confirm (default on).
    public bool ConfirmDestructive = true;
    public DateTime GiveawayStarted = DateTime.Now;
    public int GiveawayModes = 1;                          // GiveawayMode flags (default Highest)
    public int GiveawayClosestTarget = 500;
    // Race mode: everyone rolls until someone hits this exact number.
    public int GiveawayMatchTarget = 777;
    // Set to the winning roll's Id when an exact-match race is won.
    public string GiveawayMatchWinnerId = string.Empty;

    // Announce line (like Shout/Yell): one message + channel for the giveaway.
    public string GiveawayAnnounceText = string.Empty;
    public int GiveawayAnnounceChannel = 1;                // ChatChannel (default Yell)
    // Logged winners (name + when + optional note).
    public List<GiveawayWinner> GiveawayWinners = new();
    // Optional pot tracking.
    public bool GiveawayShowPot = false;
    public long GiveawayHousePot = 0;
    public List<GiveawayContribution> GiveawayContributions = new();
    // Archived past giveaways (winners + pot + contributors), kept across resets.
    public List<GiveawayHistoryEntry> GiveawayHistory = new();
    // Shared game histories (archived on reset).
    public List<GameHistoryEntry> RaffleHistory = new();
    public List<GameHistoryEntry> DeathrollHistory = new();
    public List<GameHistoryEntry> BarGameHistory = new();

    // ---- Deathroll Helper ----------------------------------------------
    public List<DeathrollPlayer> DeathrollPlayers = new();
    public List<DeathrollMatch> DeathrollMatches = new();
    public bool DeathrollBuilt = false;
    public int DeathrollKind = 0; // 0 = single elim, 1 = double elim
    public string DeathrollActiveMatch = string.Empty;
    // Roll-off range to decide who goes first. 0 = plain /random (0-999).
    public int DeathrollRolloffValue = 0;

    [NonSerialized] private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pi)
    {
        this.pluginInterface = pi;
        MigrateVenues();
        MigrateMenu();
        // Prune any runaway visit-tracking data so a huge config can't OOM on
        // load (serializing it was throwing OutOfMemoryException for heavy users).
        var pruned = PruneTrackingData();
        // Only save if pruning actually changed something; otherwise avoid a
        // full serialize on every startup (which itself was a load-time cost).
        // Guard it: a serialize failure must never stop the plugin from loading.
        if (pruned)
        {
            try { Save(); }
            catch (Exception ex)
            {
                Plugin.Log.Error(ex, "[Venue Helper] Failed to save pruned config at startup; continuing.");
            }
        }
    }

    // Caps the per-venue visit-tracking collections to sane sizes. Closed/old
    // sessions and the lowest-time visitors are dropped first. Returns true if
    // anything was removed.
    private bool PruneTrackingData()
    {
        const int maxVisitorsPerVenue = 2000;   // keep the top-N by time
        const int maxSessionsPerPlayer = 50;    // most recent sessions only
        var changed = false;

        foreach (var v in Venues)
        {
            // Cap sessions per player (keep the most recent).
            foreach (var kv in v.VisitSessions)
            {
                if (kv.Value.Count > maxSessionsPerPlayer)
                {
                    var keep = kv.Value
                        .OrderByDescending(s => s.LastSeen)
                        .Take(maxSessionsPerPlayer)
                        .OrderBy(s => s.Arrived)
                        .ToList();
                    v.VisitSessions[kv.Key] = keep;
                    changed = true;
                }
            }

            // Cap total tracked visitors (keep the highest-time ones).
            if (v.VisitSeconds.Count > maxVisitorsPerVenue)
            {
                var keepKeys = v.VisitSeconds
                    .OrderByDescending(kv => kv.Value)
                    .Take(maxVisitorsPerVenue)
                    .Select(kv => kv.Key)
                    .ToHashSet();
                v.VisitSeconds = v.VisitSeconds.Where(kv => keepKeys.Contains(kv.Key))
                    .ToDictionary(kv => kv.Key, kv => kv.Value);
                v.VisitSessions = v.VisitSessions.Where(kv => keepKeys.Contains(kv.Key))
                    .ToDictionary(kv => kv.Key, kv => kv.Value);
                changed = true;
            }
        }
        return changed;
    }

    // Ensure there's at least one menu profile, and migrate any legacy flat
    // menu items into it so existing users keep their menu.
    private void MigrateMenu()
    {
        // Run the legacy import at most once, ever. Without this guard, the old
        // QuickEmotes/QuickSays could be re-imported as macros on a later load,
        // duplicating them.
        if (!MenuMigrated)
        {
            if (MenuProfiles.Count == 0)
            {
                var profile = new MenuProfile("Default Menu");
                if (MenuItems.Count > 0)
                {
                    profile.Items = MenuItems;
                    MenuItems = new List<MenuItem>();
                }
                // Carry over old emote/say quick buttons as simple one-step macros.
                foreach (var qa in QuickEmotes.Concat(QuickSays))
                {
                    if (string.IsNullOrWhiteSpace(qa.Label) || string.IsNullOrWhiteSpace(qa.Text)) continue;
                    profile.Macros.Add(new MenuMacro(qa.Label)
                    {
                        Steps = { new ServeStep(qa.Text, 1.0f) },
                    });
                }
                if (MenuSales.Count > 0)
                {
                    profile.Sales = MenuSales;
                    MenuSales = new List<MenuSale>();
                }
                MenuProfiles.Add(profile);
            }
            // Clear the legacy sources so they can never be re-imported.
            QuickEmotes.Clear();
            QuickSays.Clear();
            MenuMigrated = true;
        }

        if (MenuProfiles.Count == 0)
            MenuProfiles.Add(new MenuProfile("Default Menu"));
        if (SelectedMenuProfile < 0 || SelectedMenuProfile >= MenuProfiles.Count)
            SelectedMenuProfile = 0;

        // Heal any duplicate macros that an earlier build may have persisted
        // (exact duplicates: same label + same step commands/waits). Keeps the
        // first of each. This is idempotent and safe to run every load.
        foreach (var profile in MenuProfiles)
        {
            var seen = new HashSet<string>();
            var deduped = new List<MenuMacro>();
            foreach (var m in profile.Macros)
            {
                var key = (m.Label ?? string.Empty) + "\u0001"
                    + string.Join("\u0002", m.Steps.Select(s => (s.Command ?? string.Empty) + "@" + s.DelayAfter));
                if (seen.Add(key))
                    deduped.Add(m);
            }
            if (deduped.Count != profile.Macros.Count)
                profile.Macros = deduped;

            // Heal duplicate menu ITEMS and their serve-steps the same way. A
            // serialization bug doubled the active menu on every save, which let
            // items/steps compound into hundreds of thousands of entries.
            foreach (var item in profile.Items)
            {
                if (item.ServeSteps.Count <= 1) continue;
                var stepSeen = new HashSet<string>();
                var stepDeduped = new List<ServeStep>();
                foreach (var s in item.ServeSteps)
                {
                    var sk = (s.Command ?? string.Empty) + "@" + s.DelayAfter;
                    if (stepSeen.Add(sk))
                        stepDeduped.Add(s);
                }
                if (stepDeduped.Count != item.ServeSteps.Count)
                    item.ServeSteps = stepDeduped;
            }

            var itemSeen = new HashSet<string>();
            var itemDeduped = new List<MenuItem>();
            foreach (var it in profile.Items)
            {
                var key = (it.Name ?? string.Empty) + "\u0001" + (it.Category ?? string.Empty)
                    + "\u0001" + it.Price + "\u0001" + (it.Emote ?? string.Empty) + "\u0001"
                    + string.Join("\u0002", it.ServeSteps.Select(s => (s.Command ?? string.Empty) + "@" + s.DelayAfter));
                if (itemSeen.Add(key))
                    itemDeduped.Add(it);
            }
            if (itemDeduped.Count != profile.Items.Count)
                profile.Items = itemDeduped;
        }
    }

    // Computed accessor for the currently-selected profile. MUST NOT be
    // serialized: it returns a full MenuProfile, and Newtonsoft would otherwise
    // write a complete second copy of the active menu on every save (this caused
    // configs to balloon to hundreds of MB and OOM on load).
    [Newtonsoft.Json.JsonIgnore]
    public MenuProfile ActiveMenuProfile
    {
        get
        {
            if (MenuProfiles.Count == 0) MenuProfiles.Add(new MenuProfile("Default Menu"));
            if (SelectedMenuProfile < 0 || SelectedMenuProfile >= MenuProfiles.Count) SelectedMenuProfile = 0;
            return MenuProfiles[SelectedMenuProfile];
        }
    }

    // One-time move of the legacy single-venue data into a default VenueProfile,
    // so existing users keep their visitor list and time database.
    private void MigrateVenues()
    {
        if (VenuesMigrated && Venues.Count > 0)
            return;

        if (Venues.Count == 0)
        {
            Venues.Add(new VenueProfile("My Venue")
            {
                AllNightSeen = AllNightSeen ?? new HashSet<string>(),
                AllNightStarted = AllNightStarted,
                VisitSeconds = VisitSeconds ?? new Dictionary<string, long>(),
                VisitSessions = VisitSessions ?? new Dictionary<string, List<VisitSession>>(),
            });
            ActiveVenue = 0;
        }
        VenuesMigrated = true;
    }

    // The currently-selected venue (always valid; creates a default if needed).
    public VenueProfile ActiveVenueProfile
    {
        get
        {
            if (Venues.Count == 0)
                Venues.Add(new VenueProfile("My Venue"));
            if (ActiveVenue < 0 || ActiveVenue >= Venues.Count)
                ActiveVenue = 0;
            return Venues[ActiveVenue];
        }
    }

    public void Save() => this.pluginInterface!.SavePluginConfig(this);

    // ---- Backup / restore ---------------------------------------------
    // Writes the entire configuration (all venues, menus, games, history, etc.)
    // to a JSON file the user chooses, so they can keep a manual backup.
    public (bool ok, string message) ExportBackup(string folder)
    {
        try
        {
            var dir = string.IsNullOrWhiteSpace(folder)
                ? this.pluginInterface!.GetPluginConfigDirectory()
                : folder.Trim();
            System.IO.Directory.CreateDirectory(dir);
            var file = System.IO.Path.Combine(dir, $"VenueHelper-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json");
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented,
                new Newtonsoft.Json.JsonSerializerSettings { TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Auto });
            System.IO.File.WriteAllText(file, json);
            return (true, $"Backup saved: {file}");
        }
        catch (Exception ex)
        {
            return (false, $"Backup failed: {ex.Message}");
        }
    }

    // Restores from a backup file, copying its values onto this config in place
    // (so the live plugin picks them up), then saves.
    public (bool ok, string message) ImportBackup(string filePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(filePath) || !System.IO.File.Exists(filePath))
                return (false, "Backup file not found. Paste the full path to a VenueHelper backup .json.");
            var json = System.IO.File.ReadAllText(filePath);
            var loaded = Newtonsoft.Json.JsonConvert.DeserializeObject<Configuration>(json,
                new Newtonsoft.Json.JsonSerializerSettings { TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Auto });
            if (loaded == null) return (false, "Could not read that backup file.");
            CopyFrom(loaded);
            Save();
            return (true, "Backup restored. All your data has been replaced from the file.");
        }
        catch (Exception ex)
        {
            return (false, $"Restore failed: {ex.Message}");
        }
    }

    // Copies every persisted public field from another config onto this one.
    // Reflection keeps this correct automatically as fields are added, and skips
    // the [NonSerialized] plugin-interface handle.
    private void CopyFrom(Configuration o)
    {
        foreach (var field in typeof(Configuration).GetFields(
                     System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
        {
            if (Attribute.IsDefined(field, typeof(NonSerializedAttribute))) continue;
            field.SetValue(this, field.GetValue(o));
        }
    }
}
