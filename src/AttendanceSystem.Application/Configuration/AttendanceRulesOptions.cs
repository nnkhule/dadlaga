namespace AttendanceSystem.Application.Configuration;

/// <summary>
/// Configurable attendance rule thresholds from appsettings.
/// </summary>
public class AttendanceRulesOptions
{
    public const string SectionName = "AttendanceRules";

    public int DefaultGraceMinutes { get; set; } = 10;
    public int AutoGeoRadiusMeters { get; set; } = 100;
    public int AutoGeoMinMinutes { get; set; } = 60;
    public int MissedCheckoutAlertRadiusMeters { get; set; } = 200;
    public int EarlyCheckinThresholdMinutes { get; set; } = 120;
    public int SuspiciousCheckinThresholdHours { get; set; } = 4;
    public int HalfDayLateThresholdMinutes { get; set; } = 180;
    public int SuspiciousDistanceMeters { get; set; } = 500;
    public int ShortShiftNoBreakHours { get; set; } = 4;
    public int MediumShiftBreakMinutes { get; set; } = 30;
    public int LongShiftBreakMinutes { get; set; } = 60;
}
