namespace AttendanceSystem.Domain.Common;

/// <summary>
/// Marker for domain events raised by aggregates.
/// </summary>
public interface IDomainEvent
{
    DateTime OccurredOn { get; }
}
