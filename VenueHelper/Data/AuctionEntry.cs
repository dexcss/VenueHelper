namespace VenueHelper.Data;

// A player currently up for auction (added by targeting them). Carries a note
// describing what they're auctioning (gpose, art, etc.) and, once sold, the
// final sale price. Moving it to history snapshots it into an AuctionRecord.
[Serializable]
public class AuctionEntry
{
    // Full name with world glyph (Name\uE05DWorld) when known, else bare name.
    public string FullName = string.Empty;

    // What they're auctioning themselves off for.
    public string Note = string.Empty;

    // Who won them at auction (the bidder). Free text - "Person X" or Name@World.
    public string Winner = string.Empty;

    // Final sale price in gil (0 = not sold / not set yet).
    public long SalePrice;

    public DateTime Added = DateTime.Now;

    public AuctionEntry() { }

    public AuctionEntry(string fullName, string note)
    {
        FullName = fullName;
        Note = note;
        Added = DateTime.Now;
    }

    public string NameOnly
    {
        get
        {
            var idx = FullName.IndexOf('\uE05D');
            return idx < 0 ? FullName : FullName[..idx];
        }
    }

    public string DisplayName => FullName.Replace('\uE05D', '@');
}

// A completed auction sale, frozen into history. The house cut is captured at
// the time of sale so changing the percentage later doesn't rewrite the past.
[Serializable]
public class AuctionRecord
{
    public string FullName = string.Empty;
    public string Note = string.Empty;
    public string Winner = string.Empty;
    public long SalePrice;
    public float HouseCutPercent;
    public DateTime When = DateTime.Now;

    public AuctionRecord() { }

    public AuctionRecord(AuctionEntry entry, float houseCutPercent)
    {
        FullName = entry.FullName;
        Note = entry.Note;
        Winner = entry.Winner;
        SalePrice = entry.SalePrice;
        HouseCutPercent = houseCutPercent;
        When = DateTime.Now;
    }

    public string NameOnly
    {
        get
        {
            var idx = FullName.IndexOf('\uE05D');
            return idx < 0 ? FullName : FullName[..idx];
        }
    }

    public string DisplayName => FullName.Replace('\uE05D', '@');

    // Gil the house keeps from this sale. Negative sales (sold TO the house,
    // e.g. -200000) take no cut \u2014 the house is paying out, not earning.
    public long HouseCut => SalePrice < 0 ? 0 : (long)Math.Round(SalePrice * (HouseCutPercent / 100.0));

    // Gil the seller takes home.
    public long Payout => SalePrice - HouseCut;
}
