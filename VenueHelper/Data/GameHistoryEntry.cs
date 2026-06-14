namespace VenueHelper.Data;

// A generic archived game result, shared by Raffle, DR Tourny, and Bar Game
// history. (Giveaway keeps its own richer GiveawayHistoryEntry.)
public class GameHistoryEntry
{
    public Guid Id = Guid.NewGuid();
    public DateTime When = DateTime.Now;
    public string Kind = string.Empty;     // e.g. "Raffle", "Tournament", game name
    public string Winner = string.Empty;   // winner name (NameOnly)
    public long Pot = 0;                    // pot/total gil, 0 if not applicable
    public string Details = string.Empty;   // extra context (runner-up, ticket count, etc.)

    public string PotShort => Pot > 0 ? Logic.GilFormat.Short(Pot) : "-";
}
