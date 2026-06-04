using AttendanceSystem.Domain.Common;

namespace AttendanceSystem.Domain.Events;

/// <summary>
/// Raised when an employee checks in.
/// </summary>
public sealed record AttendanceCheckedInEvent(Guid RecordId, Guid EmployeeId, DateTime CheckInTime) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
