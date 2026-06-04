using AttendanceSystem.Domain.Common;
using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Configurable work shift with grace period and break policy.
/// </summary>
public class WorkSchedule : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public TimeOnly ShiftStart { get; private set; }
    public TimeOnly ShiftEnd { get; private set; }
    public int GraceMinutes { get; private set; } = 10;
    public WorkDays WorkDays { get; private set; } = WorkDays.Weekdays;
    public int BreakDurationMinutes { get; private set; } = 60;
    public decimal StandardHoursPerDay { get; private set; } = 8m;
    public bool IsNightShift { get; private set; }
    public decimal NightShiftMultiplier { get; private set; } = 1.5m;
    public decimal WeekendMultiplier { get; private set; } = 2.0m;

    private WorkSchedule() { }

    public static WorkSchedule CreateStandard(string name = "Standard 9-18")
        => new()
        {
            Name = name,
            ShiftStart = new TimeOnly(9, 0),
            ShiftEnd = new TimeOnly(18, 0),
            GraceMinutes = 10,
            WorkDays = WorkDays.Weekdays,
            StandardHoursPerDay = 8m
        };

    public bool IsWorkDay(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => WorkDays.HasFlag(WorkDays.Monday),
        DayOfWeek.Tuesday => WorkDays.HasFlag(WorkDays.Tuesday),
        DayOfWeek.Wednesday => WorkDays.HasFlag(WorkDays.Wednesday),
        DayOfWeek.Thursday => WorkDays.HasFlag(WorkDays.Thursday),
        DayOfWeek.Friday => WorkDays.HasFlag(WorkDays.Friday),
        DayOfWeek.Saturday => WorkDays.HasFlag(WorkDays.Saturday),
        DayOfWeek.Sunday => WorkDays.HasFlag(WorkDays.Sunday),
        _ => false
    };
}
