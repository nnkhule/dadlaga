using AttendanceSystem.Application.Interfaces;

namespace AttendanceSystem.Application.Services;

/// <summary>
/// Haversine-based geofence calculations.
/// </summary>
public sealed class GeofenceService : IGeofenceService
{
    private const double EarthRadiusMeters = 6371000;

    /// <inheritdoc />
    public double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = DegreesToRadians(lat2 - lat1);
        var dLon = DegreesToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(DegreesToRadians(lat1)) * Math.Cos(DegreesToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusMeters * c;
    }

    /// <inheritdoc />
    public bool IsWithinRadius(double employeeLat, double employeeLon, double officeLat, double officeLon, int radiusMeters)
        => CalculateDistanceMeters(employeeLat, employeeLon, officeLat, officeLon) <= radiusMeters;

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;
}
