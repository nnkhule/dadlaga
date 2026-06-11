namespace AttendanceSystem.Application.DTOs.Auth;

public record ResetPasswordDto(string Token, string Email, string NewPassword, string ConfirmPassword);

public record ResetPasswordResponseDto(bool Success, string Message);
