namespace VenueHelper.Data;

public enum RollKind
{
    RandomPlain,   // /random  (0-999, ceiling normalized to 1000)
    RandomOutOf,   // /random N
    Dice,          // /dice N
}

public enum WinCondition
{
    SpecificNumbers,  // land exactly on one of the winning numbers
    InRange,          // land within [RangeLow, RangeHigh] inclusive
    Highest,          // highest roll among players wins
    Lowest,           // lowest roll among players wins
    ClosestTo,        // closest to a target number wins
    SurvivalStreak,   // hit a success number N times in a row (e.g. six 1s); a
                      // miss ends the run
    PrizeTiers,       // a roll is matched against a list of number-ranges, each
                      // with its own payout, plus an optional jackpot number
}

// The three survival sub-modes.
public enum SurvivalMode
{
    SameNumber,   // each roll must equal a success number (e.g. roll a 1)
    StaticHL,     // each roll must be higher/lower than a fixed threshold
    DynamicHL,    // each roll must beat the previous roll in the called direction
}

// How a survival game pays out.
public enum SurvivalPrize
{
    Fixed,      // reach StreakNeeded in a row -> flat PrizeGil / pot
    Tiered,     // each success past a threshold pays a per-step amount
    HighScore,  // best streak wins the pot (fixed or stacking) when host ends it
}

public enum PrizeKind
{
    FixedGil,         // flat gil payout
    PercentOfPot,     // a percentage of the current pot
}

// A configurable bar-game definition. Hosts build these from the modular
// options and can save several presets.
[Serializable]
public class BarGame
{
    public string Name = "New Game";

    // How players roll.
    public RollKind Roll = RollKind.RandomOutOf;
    public int RollCeiling = 100;       // for RandomOutOf / Dice

    // What wins.
    public WinCondition Condition = WinCondition.SpecificNumbers;
    public List<int> WinningNumbers = new();   // for SpecificNumbers
    public int RangeLow = 1;                    // for InRange
    public int RangeHigh = 10;                  // for InRange
    public int ClosestTarget = 50;              // for ClosestTo
    // For SurvivalStreak: roll must equal one of WinningNumbers (the "success"
    // value, e.g. 1) StreakNeeded times in a row to win; any other roll ends it.
    public int StreakNeeded = 6;
    public SurvivalMode Survival = SurvivalMode.SameNumber;
    // Static higher/lower mode: each roll must be higher (or lower) than this.
    public int StaticThreshold = 5;
    public bool StaticHigher = true;     // true = must roll higher, false = lower
    // Prize style for survival:
    public SurvivalPrize SurvivalPrizeKind = SurvivalPrize.Fixed;
    // Tiered: once past TierThreshold successes, each further success pays
    // TierPerStep gil (paid out at TierThreshold + n).
    public int TierThreshold = 3;
    public long TierPerStep = 100000;

    // ---- Prize-tier mode (WinCondition.PrizeTiers) --------------------
    // Each roll is checked against these ranges (first match wins its payout).
    public List<PrizeTier> PrizeTiers = new();
    // Optional progressive jackpot: rolling exactly JackpotNumber wins the
    // running jackpot. The jackpot starts at JackpotStart and grows by
    // JackpotPerBuyIn for each paid buy-in.
    public bool JackpotEnabled = false;
    public int JackpotNumber = 100;
    public long JackpotStart = 5000000;
    public long JackpotPerBuyIn = 0;
    public long CurrentJackpot = 0;     // live running jackpot
    public bool JackpotStarted = false; // seeded to JackpotStart on first use

    // Cost & pot.
    public long EntryCost = 0;          // gil per play (buy-in)
    public bool StackingPot = false;    // each entry adds to the pot
    public long StartingPot = 0;        // initial pot

    // Prize.
    public PrizeKind Prize = PrizeKind.PercentOfPot;
    public long PrizeGil = 0;           // for FixedGil
    public float PrizePercent = 100f;   // for PercentOfPot (default: whole pot)

    public string Notes = string.Empty; // free-form rules text

    // Live pot value for this game during play (separate from StartingPot config).
    public long CurrentPot = 0;
    // True once the pot has been actively used (entries added / reset pressed),
    // so we stop auto-syncing it to StartingPot.
    public bool PotStarted = false;

    // ---- Live play tracking (trade buy-ins + captured rolls) ----------
    // Whether roll/trade capture is currently active for this game.
    public bool Tracking = false;
    // Per-player paid plays, keyed by Name\uE05DWorld.
    public Dictionary<string, BarGamePlayer> Players = new();
    // Captured rolls, newest last.
    public List<BarGamePlay> Plays = new();

    public BarGame() { }
    public BarGame(string name) { Name = name; }
}

// One payout tier for WinCondition.PrizeTiers: a roll in [Low, High] inclusive
// pays Amount gil. Tiers are checked in list order; the first match wins.
[Serializable]
public class PrizeTier
{
    public int Low = 1;
    public int High = 100;
    public long Amount = 0;

    public PrizeTier() { }
    public PrizeTier(int low, int high, long amount) { Low = low; High = high; Amount = amount; }

    public bool Contains(int roll) => roll >= Low && roll <= High;
}
