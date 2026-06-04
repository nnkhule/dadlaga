namespace AttendanceSystem.Application.DTOs.Auth;

/// <summary>
/// Login credentials request.
/// </summary>
public record LoginRequestDto(string Email, string Password);
