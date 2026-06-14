namespace VenueHelper.Data;

// A logged giveaway winner.
public class GiveawayWinner
{
    public Guid Id = Guid.NewGuid();
    public string FullName = string.Empty; // Name\uE05DWorld when known
    public string Note = string.Empty;     // e.g. "Highest \u2014 rolled 998"
    public DateTime When = DateTime.Now;

    public string NameOnly
    {
        get
        {
            var idx = FullName.IndexOf('\uE05D');
            return idx < 0 ? FullName : FullName[..idx];
        }
    }
}

// A pot contributor (the house, or a donor who chipped in to grow the prize).
public class GiveawayContribution
{
    public Guid Id = Guid.NewGuid();
    public string Name = string.Empty; // free text (a player name, may include @World)
    public long Amount = 0;            // gil contributed
}

// A finished giveaway, archived to history. Snapshots the winners, the pot, and
// the contributors so past giveaways can be reviewed and exported.
public class GiveawayHistoryEntry
{
    public Guid Id = Guid.NewGuid();
    public DateTime When = DateTime.Now;
    public string Mode = string.Empty;        // e.g. "Highest", "Manual"
    public long HousePot = 0;
    public long TotalPot = 0;
    public List<GiveawayWinner> Winners = new();
    public List<GiveawayContribution> Contributions = new();

    public string WinnerSummary =>
        Winners.Count == 0 ? "(none)" : string.Join(", ", Winners.Select(w => w.NameOnly));
    public string ContributorSummary =>
        Contributions.Count == 0 ? "(none)" : string.Join(", ", Contributions.Select(c => $"{c.Name} {c.Amount:N0}"));
}
