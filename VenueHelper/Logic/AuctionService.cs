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
    public (bool ok, string message) Finalize(AuctionEntry entry)
    {
        if (entry.SalePrice <= 0)
            return (false, "Set a sale price greater than 0 before finalizing.");

        Config.AuctionHistory.Add(new AuctionRecord(entry, Config.HouseCutPercent));
        Config.AuctionEntries.Remove(entry);
        Config.Save();
        return (true, $"Recorded {entry.NameOnly} sold for {entry.SalePrice:N0} gil.");
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

    // ---- Totals --------------------------------------------------------

    public long TotalGilThroughHouse => Config.AuctionHistory.Sum(r => r.SalePrice);
    public long TotalHouseCut => Config.AuctionHistory.Sum(r => r.HouseCut);
    public long TotalPayouts => Config.AuctionHistory.Sum(r => r.Payout);
}
