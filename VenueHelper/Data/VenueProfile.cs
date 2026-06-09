namespace VenueHelper.Data;

// A named venue the host tracks. Each keeps its own all-night visitor set,
// start time, and lifetime time-tracking database, so someone running multiple
// venues can record each separately.
[Serializable]
public class VenueProfile
{
    public string Name = "My Venue";

    // All-night unique visitor set (Name\uE05DWorld keys) and when it started.
    public HashSet<string> AllNightSeen = new();
    public DateTime AllNightStarted = DateTime.Now;

    // Lifetime time tracking for this venue.
    public Dictionary<string, long> VisitSeconds = new();
    public Dictionary<string, List<VisitSession>> VisitSessions = new();

    public VenueProfile() { }
    public VenueProfile(string name) => Name = name;
}
