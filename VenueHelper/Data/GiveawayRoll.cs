namespace VenueHelper.Data;

// A single captured /random (or /dice) result during a giveaway. The giveaway
// only counts each player's FIRST roll, but every roll is kept in a feed so the
// host can verify what came in.
[Serializable]
public class GiveawayRoll
{
    // Stable unique id so the "counted" roll in the ordered list can be matched
    // to its twin in the feed even after save/load (reference equality breaks
    // across deserialization).
    public Guid Id = Guid.NewGuid();
    public string FullName = string.Empty; // Name\uE05DWorld when known
    public int Roll;
    public int OutOf; // the "out of" value (999 for plain /random)
    public DateTime When = DateTime.Now;
    // Set when a roll was rejected (e.g. /random N when plain-only is enforced).
    // Kept in the feed for the host to see, but never counted as an entry.
    public bool Invalid;
    public string InvalidReason = string.Empty;

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

// Which winner(s) to highlight. Multiple can be active at once (e.g. show the
// highest AND lowest roll for a two-prize giveaway).
[Flags]
public enum GiveawayMode
{
    None = 0,
    Highest = 1,
    Lowest = 2,
    Closest = 4,
    ExactMatch = 8, // "roll until someone hits the target number" race mode
    Manual = 16,    // giveaway run elsewhere (e.g. Twitch); no roll capture, just tracking
}
