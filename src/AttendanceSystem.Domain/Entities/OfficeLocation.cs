using AttendanceSystem.Domain.Common;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Geofenced office or branch location for GPS verification.
/// </summary>
public class OfficeLocation : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public int RadiusMeters { get; private set; } = 100;
    public bool IsActive { get; private set; } = true;

    private OfficeLocation() { }

    public static OfficeLocation Create(string name, double latitude, double longitude, int radiusMeters = 100)
        => new() { Name = name, Latitude = latitude, Longitude = longitude, RadiusMeters = radiusMeters };
}
