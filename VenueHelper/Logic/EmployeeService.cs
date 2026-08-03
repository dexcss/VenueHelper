namespace VenueHelper.Logic;

using VenueHelper.Data;

public class EmployeeService
{
    private readonly Plugin Plugin;
    private Configuration Config => Plugin.Configuration;

    public EmployeeService(Plugin plugin) => Plugin = plugin;

    public List<Employee> Employees => Config.Employees;

    public Employee Add(string name)
    {
        var e = new Employee { Name = string.IsNullOrWhiteSpace(name) ? "New Employee" : name.Trim() };
        Config.Employees.Add(e);
        Config.Save();
        return e;
    }

    public void Remove(Employee e)
    {
        Config.Employees.Remove(e);
        Config.Save();
    }

    // Total seconds worked this shift = completed accrued time plus the current
    // open stretch (if clocked in).
    public long WorkedSeconds(Employee e)
    {
        var secs = e.AccruedSeconds;
        if (e.ClockedInAt != null)
            secs += (long)Math.Max(0, (DateTime.Now - e.ClockedInAt.Value).TotalSeconds);
        return secs;
    }

    public void ClockIn(Employee e)
    {
        if (e.ClockedInAt != null) return;
        e.ClockedInAt = DateTime.Now;
        Config.Save();
    }

    public void ClockOut(Employee e)
    {
        if (e.ClockedInAt == null) return;
        e.AccruedSeconds += (long)Math.Max(0, (DateTime.Now - e.ClockedInAt.Value).TotalSeconds);
        e.ClockedInAt = null;
        Config.Save();
    }

    public void ToggleClock(Employee e)
    {
        if (e.ClockedInAt == null) ClockIn(e);
        else ClockOut(e);
    }

    // What the employee is owed right now (before the paid flag).
    public long AmountOwed(Employee e)
    {
        if (e.Mode == PayMode.Flat)
            return Math.Max(0, e.FlatRate);
        // Hourly: rate * hours worked.
        var hours = WorkedSeconds(e) / 3600.0;
        return (long)Math.Round(Math.Max(0, e.HourlyRate) * hours);
    }

    public void SetPaid(Employee e, bool paid)
    {
        e.Paid = paid;
        Config.Save();
    }

    // Reset one employee's shift (clears worked time + paid flag) for a new
    // night, keeping them on the roster with their pay settings.
    public void ResetShift(Employee e)
    {
        e.AccruedSeconds = 0;
        e.ClockedInAt = null;
        e.Paid = false;
        Config.Save();
    }

    public void ResetAllShifts()
    {
        foreach (var e in Config.Employees)
        {
            e.AccruedSeconds = 0;
            e.ClockedInAt = null;
            e.Paid = false;
        }
        Config.Save();
    }

    // Totals for the summary line.
    public long TotalOwed => Config.Employees.Where(e => !e.Paid).Sum(AmountOwed);
    public long TotalPaid => Config.Employees.Where(e => e.Paid).Sum(AmountOwed);
}
