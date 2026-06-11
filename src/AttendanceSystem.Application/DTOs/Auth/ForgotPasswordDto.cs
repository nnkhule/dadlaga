namespace AttendanceSystem.Application.DTOs.Auth;

public record ForgotPasswordDto(string Email);

public record ForgotPasswordResponseDto(bool Success, string Message);
