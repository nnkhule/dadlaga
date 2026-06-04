namespace AttendanceSystem.Application.DTOs.Auth;

/// <summary>
/// JWT token pair response.
/// </summary>
public record TokenResponseDto(string AccessToken, string RefreshToken, DateTime ExpiresAt, Guid? EmployeeId);
