namespace AttendanceSystem.Domain.Enums;

/// <summary>
/// How attendance was verified at check-in or check-out.
/// </summary>
public enum VerificationMethod
{
    Gps = 0,
    FaceRecognition = 1,
    Manual = 2,
    QrCode = 3,
    Nfc = 4,
    AutoGeo = 5
}
