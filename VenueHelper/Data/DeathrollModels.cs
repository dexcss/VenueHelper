namespace VenueHelper.Data;

// A bracket entrant. Name is stored with the world glyph (Name\uE05DWorld) when
// known, matching the convention used elsewhere in the plugin.
[Serializable]
public class DeathrollPlayer
{
    public string FullName = string.Empty;
    public Guid Id = Guid.NewGuid();

    public DeathrollPlayer() { }
    public DeathrollPlayer(string fullName) { FullName = fullName; }

    public string NameOnly
    {
        get
        {
            var idx = FullName.IndexOf('\uE05D');
            return idx < 0 ? FullName : FullName[..idx];
        }
    }

    public string World
    {
        get
        {
            var idx = FullName.IndexOf('\uE05D');
            return idx < 0 ? string.Empty : FullName[(idx + 1)..];
        }
    }

    public string DisplayName => FullName.Replace('\uE05D', '@');
}

public enum MatchState
{
    Pending,    // waiting on an earlier round to fill both slots
    Ready,      // both players known, not started
    InProgress, // deathroll underway
    Done,       // winner decided
}

public enum BracketKind
{
    SingleElimination,
    DoubleElimination,
}

// A single match between two players. Slots may be empty (a bye, or waiting on a
// feeder match). PlayerA/PlayerB are player Ids; resolved against the player
// list for display.
[Serializable]
public class DeathrollMatch
{
    public Guid Id = Guid.NewGuid();

    // Round number (1 = first round) and position within the round, for layout.
    public int Round;
    public int Position;

    // The two competitors (null/empty until filled).
    public Guid? PlayerA;
    public Guid? PlayerB;

    public Guid? Winner;
    public Guid? Loser;

    public MatchState State = MatchState.Pending;

    // Which match (by Id) the winner advances into, and which slot (0 = A, 1 = B).
    public Guid? WinnerTo;
    public int WinnerToSlot;

    // For double elimination: where the loser drops to (null in single elim).
    public Guid? LoserTo;
    public int LoserToSlot;

    // Bracket side: false = winners bracket (or single elim), true = losers bracket.
    public bool IsLosersBracket;

    // The live deathroll for this match (rolls so far). Rebuilt per match.
    public List<DeathrollRollRecord> Rolls = new();

    // The number the next roll should be "out of" (the previous roll's result,
    // or 0 before the opening roll which is a plain /random = 0-999).
    public int CurrentCeiling;

    // Whose turn it is by player Id, when known (the plugin tracks alternation
    // but doesn't strictly enforce it; see DeathrollManager).
    public Guid? ExpectedRoller;

    // ---- Roll-off (who goes first) ----
    // While true, the match is in the pre-deathroll roll-off: both players
    // /random the roll-off value, highest goes first, ties re-roll.
    public bool InRolloff;
    public int RolloffValueA = -1;
    public int RolloffValueB = -1;

    // Best-of-3 support (used for the final). When BestOf > 1 the match needs
    // (BestOf/2 + 1) game wins. WinsA/WinsB track games won; each game still
    // ends on a roll of 1 (loss) or 0 (win), then a new game's roll-off begins.
    public int BestOf = 1;
    public int WinsA;
    public int WinsB;
    public int CurrentGame = 1;

    // The winner-perspective game score (e.g. "2-1") recorded when the match
    // finishes; "1-0" for best-of-1.
    public string ReportedScore = string.Empty;
}

// One roll inside a match, for the live log and verification.
[Serializable]
public class DeathrollRollRecord
{
    public Guid PlayerId;
    public string PlayerName = string.Empty;
    public int Roll;
    public int OutOf;
    public DateTime When = DateTime.Now;

    // Set when the roll was rejected (wrong range or out of turn). Kept in the
    // log so the host can see why it didn't count, with a reason.
    public bool Rejected;
    public string RejectReason = string.Empty;
}
