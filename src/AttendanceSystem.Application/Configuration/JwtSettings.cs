namespace AttendanceSystem.Application.Configuration;

/// <summary>
/// JWT authentication settings.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "JwtSettings";

    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "AttendanceSystem";
    public string Audience { get; set; } = "AttendanceSystemClients";
    public int AccessTokenExpiryMinutes { get; set; } = 60;
    public int RefreshTokenExpiryDays { get; set; } = 30;
}
