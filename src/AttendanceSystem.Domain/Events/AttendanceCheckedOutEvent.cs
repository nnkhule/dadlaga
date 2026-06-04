using AttendanceSystem.Domain.Common;

namespace AttendanceSystem.Domain.Events;

/// <summary>
/// Raised when an employee checks out.
/// </summary>
public sealed record AttendanceCheckedOutEvent(Guid RecordId, Guid EmployeeId, DateTime CheckOutTime) : IDomainEvent
{
    public DateTime OccurredOn { get; } = DateTime.UtcNow;
}
