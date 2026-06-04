namespace AttendanceSystem.Domain.Enums;

/// <summary>
/// Daily attendance classification for an employee.
/// </summary>
public enum AttendanceStatus
{
    Present = 0,
    Late = 1,
    EarlyLeave = 2,
    Absent = 3,
    OnLeave = 4,
    Holiday = 5,
    NightShift = 6,
    WeekendWork = 7,
    HalfDay = 8,
    PendingManualReview = 9
}
