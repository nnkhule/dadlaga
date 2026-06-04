namespace AttendanceSystem.Domain.Enums;

/// <summary>
/// Bit flags for scheduled work days (Mon–Sun).
/// </summary>
[Flags]
public enum WorkDays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
    All = Weekdays | Saturday | Sunday
}
