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
    public static TableData RaffleSummary(IReadOnlyList<RaffleEntry> entries)
    {
        var rows = entries.Select(e => (IReadOnlyList<string>)new List<string>
        {
            e.NameOnly ?? string.Empty,
            e.World ?? string.Empty,
            e.TicketCount.ToString(),
            e.Note ?? string.Empty,
            string.Join(" ", e.TicketNumbers),
        }).ToList();

        return new TableData("Raffle Summary",
            new[] { "Name", "World", "Tickets", "Note", "TicketNumbers" }, rows);
    }

    // One row PER TICKET, e.g. "Karin Vale (1)". Single column so the TXT export
    // is one clean line per ticket for wheelofnames.com.
    // External wheel list (wheelofnames): one row per ticket, NAME ONLY - never
    // includes notes or world, so nothing private leaks to the website.
    public static TableData RaffleList(IReadOnlyList<RaffleEntry> entries)
    {
        var rows = new List<IReadOnlyList<string>>();
        foreach (var e in entries)
        {
            var count = e.TicketCount;
            for (var i = 0; i < count; i++)
                rows.Add(new List<string> { e.NameOnly });
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

    // ---- Menu ----------------------------------------------------------

    public static TableData MenuSales(IReadOnlyList<MenuSale> sales)
    {
        var rows = Enumerable.Reverse(sales)
            .Select(s => (IReadOnlyList<string>)new List<string>
            {
                s.ItemName,
                Math.Max(1, s.Quantity).ToString(),
                s.UnitPrice.ToString(),
                s.Price.ToString(),
                s.Buyer ?? string.Empty,
                s.When.ToString("yyyy-MM-dd HH:mm:ss"),
            })
            .ToList();
        return new TableData("Menu Sales", new[] { "Item", "Qty", "Unit Price", "Total", "Buyer", "Time" }, rows);
    }

    public static TableData MenuTotals(IReadOnlyList<MenuSale> sales)
    {
        var rows = sales
            .GroupBy(s => s.ItemName)
            .Select(grp => (IReadOnlyList<string>)new List<string>
            {
                grp.Key,
                // "Sold" is the number of ITEMS sold (summing quantities), not
                // the number of order rows.
                grp.Sum(s => Math.Max(1, s.Quantity)).ToString(),
                grp.Sum(s => s.Price).ToString(),
            })
            .OrderByDescending(r => long.Parse(r[2]))
            .ToList();
        return new TableData("Menu Totals", new[] { "Item", "Sold", "Revenue" }, rows);
    }

    // ---- Bar Game ------------------------------------------------------

    // Every captured roll with its result, newest first.
    public static TableData BarGameHistory(BarGame g)
    {
        var rows = new List<IReadOnlyList<string>>();
        for (var i = g.Plays.Count - 1; i >= 0; i--)
        {
            var p = g.Plays[i];
            var result = BarGameService.IsComparative(g) ? "" : (p.Won ? "WIN" : "");
            rows.Add(new List<string>
            {
                p.NameOnly,
                p.Roll.ToString(),
                $"/{(p.OutOf == 1000 ? "random" : "random " + p.OutOf)}",
                result,
                p.When.ToString("yyyy-MM-dd HH:mm:ss"),
            });
        }
        return new TableData($"{g.Name} - Rolls", new[] { "Name", "Roll", "OutOf", "Result", "Time" }, rows);
    }

    // Per-player summary: gil paid, plays bought/used.
    public static TableData BarGamePlayers(BarGame g)
    {
        var rows = g.Players.Values
            .OrderByDescending(p => p.GilPaid)
            .Select(p => (IReadOnlyList<string>)new List<string>
            {
                p.NameOnly,
                p.GilPaid.ToString(),
                p.PlaysBought(g.EntryCost).ToString(),
                p.PlaysUsed.ToString(),
            })
            .ToList();
        return new TableData($"{g.Name} - Players", new[] { "Name", "GilPaid", "PlaysBought", "PlaysUsed" }, rows);
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

    public static TableData GiveawayWinners(IReadOnlyList<Data.GiveawayWinner> winners)
    {
        var rows = winners.Select(w => (IReadOnlyList<string>)new List<string>
        {
            w.NameOnly,
            w.Note,
            w.When.ToString("yyyy-MM-dd HH:mm"),
        }).ToList();
        return new TableData("Giveaway Winners",
            new[] { "Winner", "Note", "When" }, rows);
    }

    public static TableData GiveawayContributions(IReadOnlyList<Data.GiveawayContribution> contribs, long housePot, long totalPot)
    {
        var rows = new List<IReadOnlyList<string>>
        {
            new List<string> { "House", housePot.ToString("N0") },
        };
        rows.AddRange(contribs.Select(c => (IReadOnlyList<string>)new List<string> { c.Name, c.Amount.ToString("N0") }));
        rows.Add(new List<string> { "TOTAL", totalPot.ToString("N0") });
        return new TableData("Giveaway Pot", new[] { "Contributor", "Gil" }, rows);
    }

    public static TableData GiveawayHistoryExport(IReadOnlyList<Data.GiveawayHistoryEntry> history)
    {
        var rows = history.Select(h => (IReadOnlyList<string>)new List<string>
        {
            h.When.ToString("yyyy-MM-dd HH:mm"),
            h.Mode,
            h.WinnerSummary,
            h.TotalPot.ToString("N0"),
            h.ContributorSummary,
        }).ToList();
        return new TableData("Giveaway History",
            new[] { "When", "Mode", "Winner(s)", "Total Pot", "Contributors" }, rows);
    }

    public static TableData Employees(IReadOnlyList<Data.Employee> employees, EmployeeService svc)
    {
        var rows = employees.Select(e =>
        {
            var worked = svc.WorkedSeconds(e);
            var workedText = e.Mode == Data.PayMode.Hourly ? $"{worked / 3600}h {(worked % 3600) / 60}m" : "-";
            var rate = e.Mode == Data.PayMode.Hourly ? $"{e.HourlyRate:N0}/hr" : $"{e.FlatRate:N0} flat";
            return (IReadOnlyList<string>)new List<string>
            {
                e.Name,
                e.Mode.ToString(),
                rate,
                workedText,
                svc.AmountOwed(e).ToString("N0"),
                e.Paid ? "PAID" : "unpaid",
            };
        }).ToList();
        return new TableData("Employees",
            new[] { "Name", "Mode", "Rate", "Worked", "Amount", "Status" }, rows);
    }

    public static TableData GameHistory(string title, IReadOnlyList<Data.GameHistoryEntry> history)
    {
        var rows = history.Select(h => (IReadOnlyList<string>)new List<string>
        {
            h.When.ToString("yyyy-MM-dd HH:mm"),
            h.Kind,
            h.Winner,
            h.Pot.ToString("N0"),
            h.Details,
        }).ToList();
        return new TableData(title,
            new[] { "When", "Type", "Winner", "Pot", "Details" }, rows);
    }
}
