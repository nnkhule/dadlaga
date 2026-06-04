using AttendanceSystem.Domain.Common;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Public or company holiday affecting attendance rules.
/// </summary>
public class Holiday : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public DateOnly Date { get; private set; }
    public bool IsRecurringYearly { get; private set; }
    public bool IsNationalHoliday { get; private set; }

    private Holiday() { }

    public static Holiday Create(string name, DateOnly date, bool recurring = false, bool national = true)
        => new() { Name = name, Date = date, IsRecurringYearly = recurring, IsNationalHoliday = national };
}
