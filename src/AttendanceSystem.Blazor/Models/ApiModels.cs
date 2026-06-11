using System.Text.Json.Serialization;

namespace AttendanceSystem.Blazor.Models;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);

public sealed record DashboardSummaryDto(
    int TotalEmployees,
    int ActiveEmployees,
    int PresentToday,
    int AbsentToday,
    int LateEmployees,
    int OnLeaveEmployees,
    decimal AttendanceRate,
    decimal TotalOvertimeHours);

public sealed record RecentActivityDto(Guid Id, string Type, string Title, string Message, DateTime CreatedDate);
public sealed record AttendanceTrendDto(DateOnly Date, int Present, int Absent, int Late);

public sealed record EmployeeDto(
    Guid Id,
    string EmployeeCode,
    string FullName,
    string Email,
    string? Phone,
    Guid DepartmentId,
    string? Department,
    string? DepartmentName,
    string? Position,
    DateOnly HireDate,
    string? Status,
    bool IsActive);

public sealed record EmployeeProfileDto(
    Guid Id,
    string? ProfilePhotoUrl,
    string EmployeeCode,
    string FullName,
    string Email,
    string? Phone,
    DateOnly? DateOfBirth,
    string? Department,
    string? DepartmentName,
    string? Position,
    DateOnly HireDate,
    string? EmploymentStatus,
    string? Status,
    bool IsActive);

public sealed record UpdateProfileDto(string FullName, string Email, string? Phone, DateOnly? DateOfBirth);

public sealed record DepartmentDto(
    Guid Id,
    string Name,
    string? Description,
    string? HeadEmployeeName,
    int EmployeeCount,
    bool IsActive);

public sealed record AttendanceDto(
    Guid Id,
    Guid? EmployeeId,
    string? EmployeeName,
    DateOnly Date,
    DateTime? CheckInTime,
    DateTime? CheckOutTime,
    decimal WorkHours,
    decimal Overtime,
    decimal OvertimeHours,
    decimal LateMinutes,
    string? VerificationMethod,
    string? AttendanceStatus,
    string? Status);

public sealed record AttendanceStatisticsDto(
    int PresentDays,
    int AbsentDays,
    int LateDays,
    int LeaveDays,
    decimal OvertimeHours,
    decimal AttendanceRate);

public sealed record LocationValidationRequest(double Latitude, double Longitude);
public sealed record LocationValidationDto(
    string? CurrentLocation,
    string? OfficeLocation,
    double Distance,
    double DistanceMeters,
    string? ValidationStatus,
    bool IsValid,
    bool IsWithinAllowedRadius,
    string? Message);

public sealed record AttendanceActionRequest(double? Latitude, double? Longitude, bool LocationPermissionGranted = true, string? VerificationMethod = "Gps");

public sealed record LeaveDto(
    Guid Id,
    Guid? EmployeeId,
    string? EmployeeName,
    DateOnly StartDate,
    DateOnly EndDate,
    string LeaveType,
    string? Reason,
    string? ApprovalStatus,
    string? Status,
    decimal TotalDays);

public sealed record CreateLeaveDto(DateOnly StartDate, DateOnly EndDate, string LeaveType, string Reason);

public sealed record ReportRowDto(Dictionary<string, object?> Values);

public sealed record NotificationDto(Guid Id, string Title, string Message, DateTime CreatedDate, DateTime CreatedAt, bool IsRead);

public sealed record CompanySettingsDto(string? CompanyName, string? TimeZone, string? DateFormat, string? TimeFormat, string? LogoUrl);
public sealed record AttendanceRulesDto(int GraceMinutes, bool RequireGpsForCheckIn, bool RequireGpsForCheckOut, bool AllowRemoteCheckIn, bool OvertimeEnabled);
public sealed record WorkScheduleSettingsDto(string? Name, TimeOnly? ShiftStart, TimeOnly? ShiftEnd, int BreakDurationMinutes, decimal StandardHoursPerDay);
public sealed record OfficeLocationSettingsDto(Guid Id, string Name, double Latitude, double Longitude, int RadiusMeters, bool IsActive);
public sealed record GpsSettingsDto(bool Enabled, double OfficeLatitude, double OfficeLongitude, double AllowedRadiusMeters, bool BlockOutsideRadius);

public sealed record LoginRequestDto(string Email, string Password);
public sealed record LoginResponseDto(
    [property: JsonPropertyName("accessToken")] string AccessToken,
    [property: JsonPropertyName("refreshToken")] string RefreshToken,
    DateTime ExpiresAt,
    Guid? EmployeeId);

public sealed record MessageDto(string Message);
