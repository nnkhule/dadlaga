using AttendanceSystem.Domain.Common;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Continuous GPS ping from mobile for auto geo check-in logic.
/// </summary>
public class GpsPing : BaseEntity
{
    public Guid EmployeeId { get; private set; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public DateTime RecordedAt { get; private set; }
    public DateOnly Date { get; private set; }

    public static GpsPing Create(Guid employeeId, double latitude, double longitude, DateTime recordedAt)
        => new()
        {
            EmployeeId = employeeId,
            Latitude = latitude,
            Longitude = longitude,
            RecordedAt = recordedAt,
            Date = DateOnly.FromDateTime(recordedAt)
        };
}
