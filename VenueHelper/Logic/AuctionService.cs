using VenueHelper.Data;

namespace VenueHelper.Logic;

public class AuctionService
{
    private readonly Plugin Plugin;
    private Configuration Config => Plugin.Configuration;

    public AuctionService(Plugin plugin) => Plugin = plugin;

    public List<AuctionEntry> Active => Config.AuctionEntries;
    public List<AuctionRecord> History => Config.AuctionHistory;
    public float HouseCutPercent => Config.HouseCutPercent;

    public AuctionEntry? Add(string fullName, string note)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return null;

        var normalized = fullName.Trim().Replace('@', '\uE05D');
        if (Config.AuctionEntries.Any(e => e.FullName == normalized))
            return null;

        var entry = new AuctionEntry(normalized, note);
        Config.AuctionEntries.Add(entry);
        Config.Save();
        return entry;
    }

    // Bulk-add names from a pasted clipboard list (comma/newline/semicolon/tab
    // separated). Each becomes an auction entry with no note. Returns count added.
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
            if (!Config.AuctionEntries.Any(e => e.FullName == normalized))
            {
                Config.AuctionEntries.Add(new AuctionEntry(normalized, string.Empty));
                added++;
            }
        }
        if (added > 0) Config.Save();
        return added;
    }

    public void Remove(AuctionEntry entry)
    {
        Config.AuctionEntries.Remove(entry);
        Config.Save();
    }

    public void ClearActive()
    {
        Config.AuctionEntries.Clear();
        Config.Save();
    }

    public void SetHouseCut(float percent)
    {
        Config.HouseCutPercent = Math.Clamp(percent, 0f, 100f);
        Config.Save();
    }

    public void Save() => Config.Save();

    // Move a finished entry into history, snapshotting the current house cut %.
    // A sale price of 0 is rejected, but negatives are allowed: a negative price
    // means the item was sold TO the house (the house pays out), which takes no
    // house cut.
    public (bool ok, string message) Finalize(AuctionEntry entry)
    {
        if (entry.SalePrice == 0)
            return (false, "Set a sale price before finalizing (use a negative price for a sale to the house).");

        Config.AuctionHistory.Add(new AuctionRecord(entry, Config.HouseCutPercent));
        Config.AuctionEntries.Remove(entry);
        Config.Save();
        var verb = entry.SalePrice < 0 ? "sold to house for" : "sold for";
        return (true, $"Recorded {entry.NameOnly} {verb} {entry.SalePrice:N0} gil.");
    }

    public void RemoveHistory(AuctionRecord record)
    {
        Config.AuctionHistory.Remove(record);
        Config.Save();
    }

    public void ClearHistory()
    {
        Config.AuctionHistory.Clear();
        Config.Save();
    }

    // History filtered to an optional inclusive date range (by sale date). Null
    // bounds mean unbounded on that side.
    public List<AuctionRecord> HistoryBetween(DateTime? from, DateTime? to)
    {
        IEnumerable<AuctionRecord> q = Config.AuctionHistory;
        if (from.HasValue) q = q.Where(r => r.When.Date >= from.Value.Date);
        if (to.HasValue) q = q.Where(r => r.When.Date <= to.Value.Date);
        return q.OrderByDescending(r => r.When).ToList();
    }

    // ---- Totals --------------------------------------------------------

    public long TotalGilThroughHouse => Config.AuctionHistory.Sum(r => r.SalePrice);
    public long TotalHouseCut => Config.AuctionHistory.Sum(r => r.HouseCut);
    public long TotalPayouts => Config.AuctionHistory.Sum(r => r.Payout);

    // ---- Buyer tracking (manual aliases) -------------------------------

    public List<BuyerProfile> Buyers => Config.Buyers;

    public BuyerProfile AddBuyer(string displayName)
    {
        var b = new BuyerProfile(displayName.Trim());
        Config.Buyers.Add(b);
        Config.Save();
        return b;
    }

    public void RemoveBuyer(BuyerProfile b)
    {
        Config.Buyers.Remove(b);
        Config.Save();
    }

    public void AddAlias(BuyerProfile b, string alias)
    {
        alias = alias.Trim();
        if (alias.Length == 0) return;
        if (!b.Aliases.Any(a => a.Equals(alias, StringComparison.OrdinalIgnoreCase)))
            b.Aliases.Add(alias);
        Config.Save();
    }

    public void RemoveAlias(BuyerProfile b, string alias)
    {
        b.Aliases.RemoveAll(a => a.Equals(alias, StringComparison.OrdinalIgnoreCase));
        Config.Save();
    }

    // Total a buyer spent across all their aliases (matched against the Winner
    // field of history records). Only positive sales count as spend.
    public long SpendForBuyer(BuyerProfile b)
    {
        return Config.AuctionHistory
            .Where(r => r.SalePrice > 0 && BuyerMatches(b, r.Winner))
            .Sum(r => r.SalePrice);
    }

    public int PurchaseCountForBuyer(BuyerProfile b) =>
        Config.AuctionHistory.Count(r => r.SalePrice > 0 && BuyerMatches(b, r.Winner));

    private static bool BuyerMatches(BuyerProfile b, string winner)
    {
        if (string.IsNullOrWhiteSpace(winner)) return false;
        var w = winner.Trim();
        if (b.DisplayName.Equals(w, StringComparison.OrdinalIgnoreCase)) return true;
        return b.Aliases.Any(a =>
            w.Equals(a, StringComparison.OrdinalIgnoreCase) ||
            w.Contains(a, StringComparison.OrdinalIgnoreCase));
    }
}
