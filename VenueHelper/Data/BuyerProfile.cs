namespace VenueHelper.Data;

// A buyer the host wants to track across multiple names/alts. The host types
// in which winner-names belong to this person; spend is aggregated across all
// of them from the auction history.
[Serializable]
public class BuyerProfile
{
    public string DisplayName = string.Empty;     // canonical label, e.g. "Barcode"
    public List<string> Aliases = new();          // winner strings that map to them

    public BuyerProfile() { }
    public BuyerProfile(string displayName) => DisplayName = displayName;
}
