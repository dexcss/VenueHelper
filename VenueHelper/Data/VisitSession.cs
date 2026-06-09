namespace VenueHelper.Data;

// A single visit. Arrived = first seen. LastSeen = the most recent scan that
// actually saw the player. Left = the confirmed departure time, which is only
// finalized once the player has been absent for the grace period (so it equals
// LastSeen, but isn't "official" until grace elapses). Open = session still
// counts as ongoing (present, or absent but within grace).
[Serializable]
public class VisitSession
{
    public DateTime Arrived;
    public DateTime LastSeen;  // most recent scan that saw them
    public DateTime Left;      // confirmed departure (finalized after grace)
    public bool Open = true;   // still present or within grace window

    public VisitSession() { }
    public VisitSession(DateTime arrived)
    {
        Arrived = arrived;
        LastSeen = arrived;
        Left = arrived;
        Open = true;
    }

    public TimeSpan Duration => (Open ? LastSeen : Left) - Arrived;
}
