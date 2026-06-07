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
    public HashSet<string> AllNightSeen = new();
    public DateTime AllNightStarted = DateTime.Now;
    public bool AllNightRunning = false;

    // ---- Raffle Helper -------------------------------------------------
    public List<RaffleEntry> RaffleEntries = new();
    // Gil cost of a single ticket (used when auto-crediting detected trades).
    public long TicketCost = 100000;
    // When a trade is detected, auto-credit it to the raffle as tickets.
    public bool RaffleAutoTrade = true;
    // House cut percent for raffles (e.g. 20 for an 80/20 split). Whole numbers.
    public float RaffleHouseCutPercent = 0f;

    // ---- Auction Helper ------------------------------------------------
    public List<AuctionEntry> AuctionEntries = new();
    public List<AuctionRecord> AuctionHistory = new();
    // Percentage the house keeps from each sale (0-100).
    public float HouseCutPercent = 0f;
    // Tracked buyers and their aliases (for "how much did X spend" across alts).
    public List<BuyerProfile> Buyers = new();

    // ---- Giveaway Helper -----------------------------------------------
    // Persisted so a crash mid-giveaway doesn't lose the rolls. Cleared only
    // when the host hits Reset (or Start, which begins a fresh round).
    public List<GiveawayRoll> GiveawayOrdered = new();   // first roll per player
    public List<GiveawayRoll> GiveawayFeed = new();      // every roll, newest first
    public bool GiveawayRunning = false;
    public DateTime GiveawayStarted = DateTime.Now;
    public int GiveawayModes = 1;                          // GiveawayMode flags (default Highest)
    public int GiveawayClosestTarget = 500;
    // Race mode: everyone rolls until someone hits this exact number.
    public int GiveawayMatchTarget = 777;
    // Set to the winning roll's Id when an exact-match race is won.
    public string GiveawayMatchWinnerId = string.Empty;

    // ---- Deathroll Helper ----------------------------------------------
    public List<DeathrollPlayer> DeathrollPlayers = new();
    public List<DeathrollMatch> DeathrollMatches = new();
    public bool DeathrollBuilt = false;
    public int DeathrollKind = 0; // 0 = single elim, 1 = double elim
    public string DeathrollActiveMatch = string.Empty;
    // Roll-off range to decide who goes first. 0 = plain /random (0-999).
    public int DeathrollRolloffValue = 0;

    [NonSerialized] private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pi) => this.pluginInterface = pi;

    public void Save() => this.pluginInterface!.SavePluginConfig(this);
}
