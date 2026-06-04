namespace AttendanceSystem.Domain.Enums;

/// <summary>
/// Categories of suspicious attendance activity.
/// </summary>
public enum SuspiciousAlertType
{
    RemoteCheckIn = 0,
    UnusualTime = 1,
    MultipleAttempts = 2,
    FaceRecognitionFailures = 3,
    NewDevice = 4,
    MissingPreviousCheckout = 5,
    VeryEarlyCheckIn = 6
}
