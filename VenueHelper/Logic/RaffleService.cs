using VenueHelper.Data;

namespace VenueHelper.Logic;

public class RaffleService
{
    private readonly Plugin Plugin;
    private Configuration Config => Plugin.Configuration;

    public RaffleService(Plugin plugin) => Plugin = plugin;

    public List<RaffleEntry> Entries => Config.RaffleEntries;
    public long TicketCost => Config.TicketCost;

    // ---- Entry management ----------------------------------------------

    // Find an entry by full name, matching on bare name if the world tag differs
    // (trades sometimes arrive without a world).
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
            // Upgrade a bare-name entry to a full Name@World one if we learn the world.
            if (!existing.FullName.Contains('\uE05D') && fullName.Contains('\uE05D'))
                existing.FullName = fullName;
            return existing;
        }

        var entry = new RaffleEntry(fullName);
        Config.RaffleEntries.Add(entry);
        Config.Save();
        return entry;
    }

    // Add a player by typed "Name@World" or by the world-glyph form.
    public RaffleEntry AddManual(string name)
    {
        var normalized = name.Trim().Replace('@', '\uE05D');
        return GetOrCreate(normalized);
    }

    public void CreditGil(string fullName, long gil)
    {
        if (gil <= 0)
            return;
        var entry = GetOrCreate(fullName);
        entry.GilPaid += gil;
        Config.Save();
    }

    public void SetGil(string fullName, long gil)
    {
        var entry = GetOrCreate(fullName);
        entry.GilPaid = Math.Max(0, gil);
        Config.Save();
    }

    public void AddManualTickets(RaffleEntry entry, int count)
    {
        entry.ManualTickets = Math.Max(0, entry.ManualTickets + count);
        Config.Save();
    }

    public void Remove(RaffleEntry entry)
    {
        Config.RaffleEntries.Remove(entry);
        Config.Save();
    }

    public void Reset()
    {
        Config.RaffleEntries.Clear();
        Config.Save();
    }

    public void SetTicketCost(long cost)
    {
        Config.TicketCost = Math.Max(1, cost);
        Config.Save();
    }

    // ---- Ticket number assignment --------------------------------------

    public int TotalTickets => Config.RaffleEntries.Sum(e => e.TicketCount(Config.TicketCost));

    // Assign sequential numbers starting at 1, in current list order, giving
    // each player a contiguous block sized to their ticket count. Caps at 999.
    public (bool ok, string message) AssignSequential()
    {
        var total = TotalTickets;
        if (total == 0)
            return (false, "No tickets to assign. Add buy-ins first.");
        if (total > 999)
            return (false, $"{total} tickets exceeds the 1-999 range. Reduce buy-ins or raise the ticket cost.");

        var next = 1;
        foreach (var e in Config.RaffleEntries)
        {
            var count = e.TicketCount(Config.TicketCost);
            e.TicketNumbers = new List<int>();
            for (var i = 0; i < count; i++)
                e.TicketNumbers.Add(next++);
        }
        Config.Save();
        return (true, $"Assigned numbers 1-{next - 1} across {Config.RaffleEntries.Count} players.");
    }

    // Assign numbers 1..total randomly shuffled, then hand out blocks in list
    // order. Each player still gets a set of unique numbers, just not contiguous.
    public (bool ok, string message) AssignShuffled()
    {
        var total = TotalTickets;
        if (total == 0)
            return (false, "No tickets to assign. Add buy-ins first.");
        if (total > 999)
            return (false, $"{total} tickets exceeds the 1-999 range. Reduce buy-ins or raise the ticket cost.");

        var pool = Enumerable.Range(1, total).ToList();
        var rng = new Random();
        for (var i = pool.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        var idx = 0;
        foreach (var e in Config.RaffleEntries)
        {
            var count = e.TicketCount(Config.TicketCost);
            e.TicketNumbers = new List<int>();
            for (var i = 0; i < count; i++)
                e.TicketNumbers.Add(pool[idx++]);
            e.TicketNumbers.Sort();
        }
        Config.Save();
        return (true, $"Randomly assigned {total} ticket numbers.");
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
