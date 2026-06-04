namespace AttendanceSystem.Application.Interfaces;

/// <summary>
/// GPS distance validation using Haversine formula.
/// </summary>
public interface IGeofenceService
{
    double CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2);
    bool IsWithinRadius(double employeeLat, double employeeLon, double officeLat, double officeLon, int radiusMeters);
}
