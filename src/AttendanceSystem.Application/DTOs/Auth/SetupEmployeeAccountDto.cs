namespace AttendanceSystem.Application.DTOs.Auth;

public record SetupEmployeeAccountDto(string EmployeeId, string Email, string Password, string? FullName);
