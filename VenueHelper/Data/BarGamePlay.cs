namespace VenueHelper.Data;

// One captured play in a bar game: a player's roll and whether it won.
[Serializable]
public class BarGamePlay
{
    public string FullName = string.Empty;  // Name\uE05DWorld
    public int Roll;
    public int OutOf;        // normalized ceiling the roll was out of
    public bool Won;
    public DateTime When = DateTime.Now;

    public BarGamePlay() { }
    public BarGamePlay(string fullName, int roll, int outOf, bool won)
    {
        FullName = fullName;
        Roll = roll;
        OutOf = outOf;
        Won = won;
        When = DateTime.Now;
    }

    public string NameOnly
    {
        get
        {
            var idx = FullName.IndexOf('\uE05D');
            return idx < 0 ? FullName : FullName[..idx];
        }
    }
}

// Tracks a player's paid plays (from trades) and how many they've used, for the
// active bar game.
[Serializable]
public class BarGamePlayer
{
    public string FullName = string.Empty;
    public long GilPaid;       // total gil traded in
    public int PlaysUsed;      // rolls consumed
    // For survival-streak games: current consecutive successes, and whether
    // this player's run has already ended (busted or won).
    public int Streak;
    public bool StreakBusted;
    public bool StreakWon;
    public int LastRoll = -1;        // previous roll, for dynamic higher/lower
    public int PendingCall;          // host's call for the next dynamic roll: +1 higher, -1 lower, 0 none
    public long TierWinnings;        // gil earned so far in a tiered survival run
    public int BestStreak;           // best streak achieved (for high-score mode)
    // Free-game roll accounting: one roll per player by default; the host can
    // grant extra rolls (FreeRollsGranted) via the freebie/manual buttons.
    public int FreeRollsUsed;
    public int FreeRollsGranted;

    public BarGamePlayer() { }
    public BarGamePlayer(string fullName) => FullName = fullName;

    public string NameOnly
    {
        get
        {
            var idx = FullName.IndexOf('\uE05D');
            return idx < 0 ? FullName : FullName[..idx];
        }
    }

    // How many plays they've bought given an entry cost.
    public int PlaysBought(long entryCost) => entryCost > 0 ? (int)(GilPaid / entryCost) : 0;
    public int PlaysRemaining(long entryCost) => Math.Max(0, PlaysBought(entryCost) - PlaysUsed);
}
