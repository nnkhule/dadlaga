namespace AttendanceSystem.Domain.Enums;

/// <summary>
/// Admin review status for suspicious activity alerts.
/// </summary>
public enum ReviewStatus
{
    Pending = 0,
    Approved = 1,
    Investigating = 2,
    Dismissed = 3
}
