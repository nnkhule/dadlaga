namespace AttendanceSystem.Domain.Enums;

/// <summary>
/// Approval workflow status for leave and time adjustment requests.
/// </summary>
public enum RequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3
}
