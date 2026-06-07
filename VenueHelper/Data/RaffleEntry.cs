namespace VenueHelper.Data;

// One raffle participant: identity, how many tickets they bought, an optional
// note (e.g. Discord handle), and the ticket numbers assigned to them.
[Serializable]
public class RaffleEntry
{
    // Full name with the world-separator glyph (Name\uE05DWorld) when known,
    // otherwise just the bare name.
    public string FullName = string.Empty;

    // Tickets this player bought (entered directly by the host).
    public int Tickets;

    // Free-form note (Discord name, etc.). Not included in external/wheel export.
    public string Note = string.Empty;

    // Assigned raffle numbers (0-999). Empty until assigned, or left empty if
    // the draw overflowed past 999 (host uses wheelofnames instead).
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

    public string DisplayName => FullName.Replace('\uE05D', '@');

    public int TicketCount => Math.Max(0, Tickets);
}
