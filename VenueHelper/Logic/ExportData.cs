using VenueHelper.Data;

namespace VenueHelper.Logic;

// Builds TableData datasets for each exportable view. The actual file writing
// (TXT/CSV/PDF/XLSX) is handled by Exporter.
public static class ExportData
{
    private static string NameOf(string? full)
    {
        full ??= string.Empty;
        var idx = full.IndexOf('\uE05D');
        return idx < 0 ? full : full[..idx];
    }

    private static string WorldOf(string? full)
    {
        full ??= string.Empty;
        var idx = full.IndexOf('\uE05D');
        return idx < 0 ? string.Empty : full[(idx + 1)..];
    }

    // ---- Raffle --------------------------------------------------------

    // Summary: one row per player.
    public static TableData RaffleSummary(IReadOnlyList<RaffleEntry> entries, long ticketCost)
    {
        var rows = entries.Select(e => (IReadOnlyList<string>)new List<string>
        {
            e.NameOnly ?? string.Empty,
            e.World ?? string.Empty,
            e.GilPaid.ToString(),
            e.TicketCount(ticketCost).ToString(),
            string.Join(" ", e.TicketNumbers),
        }).ToList();

        return new TableData("Raffle Summary",
            new[] { "Name", "World", "GilPaid", "Tickets", "TicketNumbers" }, rows);
    }

    // One row PER TICKET, e.g. "Karin Vale (1)". Single column so the TXT export
    // is one clean line per ticket for wheelofnames.com.
    public static TableData RaffleList(IReadOnlyList<RaffleEntry> entries, long ticketCost)
    {
        var rows = new List<IReadOnlyList<string>>();
        foreach (var e in entries)
        {
            if (e.TicketNumbers.Count > 0)
            {
                foreach (var n in e.TicketNumbers)
                    rows.Add(new List<string> { $"{e.NameOnly} ({n})" });
            }
            else
            {
                var count = e.TicketCount(ticketCost);
                for (var i = 0; i < count; i++)
                    rows.Add(new List<string> { e.NameOnly });
            }
        }
        return new TableData("Raffle Tickets", new[] { "Ticket" }, rows);
    }

    // ---- Venue Counter -------------------------------------------------

    public static TableData Visitors(IEnumerable<string> visitors)
    {
        var rows = visitors
            .OrderBy(NameOf, StringComparer.OrdinalIgnoreCase)
            .Select(v => (IReadOnlyList<string>)new List<string> { NameOf(v), WorldOf(v) })
            .ToList();
        return new TableData("Unique Visitors", new[] { "Name", "World" }, rows);
    }

    // ---- Auction -------------------------------------------------------

    public static TableData AuctionHistory(IReadOnlyList<AuctionRecord> history)
    {
        var rows = history.Select(r => (IReadOnlyList<string>)new List<string>
        {
            r.When.ToString("yyyy-MM-dd HH:mm:ss"),
            r.NameOnly ?? string.Empty,
            WorldOf(r.FullName ?? string.Empty),
            r.Note ?? string.Empty,
            r.Winner ?? string.Empty,
            r.SalePrice.ToString(),
            r.HouseCutPercent.ToString("0"),
            r.HouseCut.ToString(),
            r.Payout.ToString(),
        }).ToList();

        return new TableData("Auction History",
            new[] { "Time", "Name", "World", "Note", "WonBy", "SalePrice", "HouseCutPercent", "HouseCut", "Payout" }, rows);
    }

    // ---- Giveaway ------------------------------------------------------

    public static TableData GiveawayResults(IReadOnlyList<Data.GiveawayRoll> rolls)
    {
        var rows = rolls.Select(r => (IReadOnlyList<string>)new List<string>
        {
            r.NameOnly ?? string.Empty,
            WorldOf(r.FullName),
            r.Roll.ToString(),
            r.OutOf.ToString(),
            r.When.ToString("HH:mm:ss"),
        }).ToList();

        return new TableData("Giveaway Rolls",
            new[] { "Name", "World", "Roll", "OutOf", "Time" }, rows);
    }
}
