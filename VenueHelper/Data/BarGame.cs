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
