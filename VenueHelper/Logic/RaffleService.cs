using VenueHelper.Data;

namespace VenueHelper.Logic;

public class RaffleService
{
    private readonly Plugin Plugin;
    private Configuration Config => Plugin.Configuration;

    public RaffleService(Plugin plugin) => Plugin = plugin;

    public List<RaffleEntry> Entries => Config.RaffleEntries;

    // House cut percent for raffles (whole-number percent), e.g. 20 for an 80/20.
    public float HouseCutPercent => Config.RaffleHouseCutPercent;
    public void SetHouseCut(float pct)
    {
        Config.RaffleHouseCutPercent = (float)Math.Round(Math.Clamp(pct, 0f, 100f));
        Config.Save();
    }

    // ---- Entry management ----------------------------------------------

    public RaffleEntry? Find(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return null;
        var direct = Config.RaffleEntries.FirstOrDefault(e => e.FullName == fullName);
        if (direct != null)
            return direct;
        var bare = StripWorld(fullName);
        return Config.RaffleEntries.FirstOrDefault(e => StripWorld(e.FullName) == bare);
    }

    public RaffleEntry GetOrCreate(string fullName)
    {
        var existing = Find(fullName);
        if (existing != null)
        {
            if (!existing.FullName.Contains('\uE05D') && fullName.Contains('\uE05D'))
                existing.FullName = fullName;
            return existing;
        }
        var entry = new RaffleEntry(fullName);
        Config.RaffleEntries.Add(entry);
        Config.Save();
        return entry;
    }

    public RaffleEntry AddManual(string name)
    {
        var normalized = name.Trim().Replace('@', '\uE05D');
        return GetOrCreate(normalized);
    }

    // Add (or subtract) tickets for a player. Tickets never go below 0 \u2014 and
    // since tickets are a single manual number now, the minus button can always
    // walk it all the way back down to 0 (no hidden floor).
    public void AddTickets(RaffleEntry entry, int delta)
    {
        entry.Tickets = Math.Max(0, entry.Tickets + delta);
        Config.Save();
    }

    public void SetTickets(RaffleEntry entry, int count)
    {
        entry.Tickets = Math.Max(0, count);
        Config.Save();
    }

    // Add (or remove) free/comp tickets \u2014 they enter the draw but don't add
    // to the pot.
    public void AddFreeTickets(RaffleEntry entry, int delta)
    {
        entry.FreeTickets = Math.Max(0, entry.FreeTickets + delta);
        Config.Save();
    }

    // Bulk-add a list of names (from a pasted clipboard list, comma/newline
    // separated). Returns how many were newly added.
    public int ImportNames(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0;
        var parts = raw.Split(new[] { '\n', '\r', ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var added = 0;
        foreach (var p in parts)
        {
            var name = p.Trim();
            if (name.Length == 0) continue;
            var normalized = name.Replace('@', '\uE05D');
            if (Find(normalized) == null)
            {
                Config.RaffleEntries.Add(new RaffleEntry(normalized));
                added++;
            }
        }
        if (added > 0) Config.Save();
        return added;
    }

    public void Remove(RaffleEntry entry)
    {
        Config.RaffleEntries.Remove(entry);
        Config.Save();
    }

    public void Reset()
    {
        ArchiveCurrent();
        Config.RaffleEntries.Clear();
        Config.RaffleWinner = string.Empty;
        Config.Save();
    }

    // ---- Winner + history ----------------------------------------------

    public string Winner
    {
        get => Config.RaffleWinner;
        set { Config.RaffleWinner = value ?? string.Empty; Config.Save(); }
    }

    public long Pot => (long)TotalPaidTickets * Config.TicketCost;
    public long HouseTake => (long)Math.Round(Pot * (HouseCutPercent / 100.0));
    public long WinnerPayout => Pot - HouseTake;

    public IReadOnlyList<Data.GameHistoryEntry> History => Config.RaffleHistory;

    // Snapshot the current raffle into history if there's anything to keep.
    public bool ArchiveCurrent()
    {
        if (Config.RaffleEntries.Count == 0 && string.IsNullOrWhiteSpace(Config.RaffleWinner))
            return false;
        var winnerName = string.IsNullOrWhiteSpace(Config.RaffleWinner) ? "(undrawn)" : NameOnly(Config.RaffleWinner);
        Config.RaffleHistory.Insert(0, new Data.GameHistoryEntry
        {
            When = DateTime.Now,
            Kind = "Raffle",
            Winner = winnerName,
            Pot = Pot,
            Details = $"{TotalTickets} tickets, {Config.RaffleEntries.Count} players, payout {GilFormat.Short(WinnerPayout)} (house {HouseCutPercent:0}%)",
        });
        Config.Save();
        return true;
    }

    public void ClearHistory()
    {
        Config.RaffleHistory.Clear();
        Config.Save();
    }

    private static string NameOnly(string full)
    {
        var idx = full.IndexOf('\uE05D');
        return idx < 0 ? full : full[..idx];
    }

    // ---- Ticket number assignment --------------------------------------

    public int TotalTickets => Config.RaffleEntries.Sum(e => e.TicketCount);
    // Paid tickets only (for the pot value).
    public int TotalPaidTickets => Config.RaffleEntries.Sum(e => e.PaidTickets);

    // The raffle is "claiming trades" when auto-credit is on and it has entries
    // (i.e. a raffle is in progress). Used to warn against running a bar game's
    // trade capture at the same time, since both can't take trades at once.
    public bool IsClaimingTrades => Config.RaffleAutoTrade && Config.RaffleEntries.Count > 0;

    // Assign numbers starting at 0 in list order, each player getting a
    // contiguous block. If the total exceeds 1000 (0-999), numbers are left
    // blank and the host is told to use wheelofnames instead.
    public (bool ok, string message) AssignSequential()
    {
        var total = TotalTickets;
        if (total == 0)
            return (false, "No tickets to assign. Add buy-ins first.");
        if (total > 1000)
        {
            ClearNumbers();
            return (false, $"{total} tickets exceeds 0-999. Numbers left blank \u2014 use the external wheel (wheelofnames) for the draw.");
        }

        var next = 0;
        foreach (var e in Config.RaffleEntries)
        {
            e.TicketNumbers = new List<int>();
            for (var i = 0; i < e.TicketCount; i++)
                e.TicketNumbers.Add(next++);
        }
        Config.Save();
        return (true, $"Assigned numbers 0-{next - 1} across {Config.RaffleEntries.Count} players.");
    }

    public (bool ok, string message) AssignShuffled()
    {
        var total = TotalTickets;
        if (total == 0)
            return (false, "No tickets to assign. Add buy-ins first.");
        if (total > 1000)
        {
            ClearNumbers();
            return (false, $"{total} tickets exceeds 0-999. Numbers left blank \u2014 use the external wheel (wheelofnames) for the draw.");
        }

        var pool = Enumerable.Range(0, total).ToList();
        var rng = new Random();
        for (var i = pool.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        var idx = 0;
        foreach (var e in Config.RaffleEntries)
        {
            e.TicketNumbers = new List<int>();
            for (var i = 0; i < e.TicketCount; i++)
                e.TicketNumbers.Add(pool[idx++]);
            e.TicketNumbers.Sort();
        }
        Config.Save();
        return (true, $"Randomly assigned {total} ticket numbers (0-{total - 1}).");
    }

    public void ClearNumbers()
    {
        foreach (var e in Config.RaffleEntries)
            e.TicketNumbers.Clear();
        Config.Save();
    }

    private static string StripWorld(string full)
    {
        var idx = full.IndexOf('\uE05D');
        return idx < 0 ? full : full[..idx];
    }
}
