namespace VenueHelper.Data;

public enum PayMode
{
    Hourly, // paid by time worked (CurrentShiftStart..now + accrued)
    Flat,   // a fixed amount per shift/night
}

// A tracked staff member. Persists across nights (roster is remembered); the
// per-night worked time and paid flag are reset by the host when they choose.
[Serializable]
public class Employee
{
    public Guid Id = Guid.NewGuid();
    public string Name = string.Empty;
    public PayMode Mode = PayMode.Hourly;

    public long HourlyRate = 0;   // gil per hour (for Hourly)
    public long FlatRate = 0;     // gil for the night (for Flat)

    // Dynamic (hourly) time tracking. AccruedSeconds is completed time; when
    // ClockedInAt is set the employee is currently on the clock.
    public long AccruedSeconds = 0;
    public DateTime? ClockedInAt = null;

    public bool Paid = false;

    [Newtonsoft.Json.JsonIgnore]
    public bool IsClockedIn => ClockedInAt != null;
}
