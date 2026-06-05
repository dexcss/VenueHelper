namespace VenueHelper.Data;

// One raffle participant: their identity, total gil paid in, and the ticket
// numbers assigned to them. Tickets are computed from gil paid divided by the
// configured ticket cost, but can also be added manually.
[Serializable]
public class RaffleEntry
{
    // Full name with the world-separator glyph (Name\uE05DWorld) when known,
    // otherwise just the bare name.
    public string FullName = string.Empty;

    // Total gil this player has paid in toward raffle tickets.
    public long GilPaid;

    // Tickets bought manually (added on top of whatever gil/cost computes).
    public int ManualTickets;

    // Assigned raffle numbers (1-999). Empty until the host assigns them.
    public List<int> TicketNumbers = new();

    public DateTime FirstSeen = DateTime.Now;

    public RaffleEntry() { }

    public RaffleEntry(string fullName)
    {
        FullName = fullName;
        FirstSeen = DateTime.Now;
    }

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

    // "Name@World" for display and CSV.
    public string DisplayName => FullName.Replace('\uE05D', '@');

    // Number of tickets a given gil amount buys, plus any manual tickets.
    public int TicketCount(long ticketCost)
    {
        var fromGil = ticketCost <= 0 ? 0 : (int)(GilPaid / ticketCost);
        return fromGil + ManualTickets;
    }
}
