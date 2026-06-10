using AttendanceSystem.Application.DTOs.Auth;
using AttendanceSystem.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.API.Controllers;

internal static class Roles
{
    public const string Employee = "Employee";
    public const string Manager = "Manager";
    public const string Hr = "HR";
    public const string Admin = "Admin";
    public const string SuperAdmin = "SuperAdmin";
    public const string HrOrAdmin = $"{Hr},{Admin}";
    public const string ManagerHrOrAdmin = $"{Manager},{Hr},{Admin}";
    public const string AdminOrSuperAdmin = $"{Admin},{SuperAdmin}";
}

public abstract class ContractControllerBase : ControllerBase
{
    protected ObjectResult Contract(string level, string purpose, string requestDto, string responseDto, string authorization, params string[] validationRules)
        => StatusCode(StatusCodes.Status501NotImplemented, new EndpointContractResponse(
            level,
            purpose,
            requestDto,
            responseDto,
            authorization,
            validationRules));
}

public sealed record EndpointContractResponse(
    string Level,
    string Purpose,
    string RequestDto,
    string ResponseDto,
    string Authorization,
    IReadOnlyList<string> ValidationRules);

public sealed record MessageResponseDto(string Message);
public sealed record PagedResponseDto<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount);
public sealed record DateRangeQueryDto(DateOnly? From, DateOnly? To, int PageNumber = 1, int PageSize = 20);
public sealed record ApprovalRequestDto(string? Comment);
public sealed record RejectionRequestDto(string Reason);
public sealed record FileExportQueryDto(DateOnly From, DateOnly To, Guid? DepartmentId, Guid? EmployeeId);

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthV1Controller : ContractControllerBase
{
    private readonly JwtTokenService _jwtTokenService;

    public AuthV1Controller(JwtTokenService jwtTokenService) => _jwtTokenService = jwtTokenService;

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _jwtTokenService.LoginAsync(request.Email, request.Password, cancellationToken);
        return result is null ? Unauthorized(new { message = "Invalid credentials." }) : Ok(result);
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _jwtTokenService.RefreshAsync(request.RefreshToken, cancellationToken);
        return result is null ? Unauthorized() : Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout([FromBody] LogoutRequestDto request)
        => Contract("MVP", "Revoke the current refresh token/session.", nameof(LogoutRequestDto), nameof(MessageResponseDto), "Authenticated", "Refresh token is required.");

    [HttpPost("change-password")]
    [Authorize]
    public IActionResult ChangePassword([FromBody] ChangePasswordRequestDto request)
        => Contract("MVP", "Change the authenticated user's password.", nameof(ChangePasswordRequestDto), nameof(MessageResponseDto), "Authenticated", "Current password is required.", "New password must satisfy password policy.", "Confirm password must match.");

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public IActionResult ForgotPassword([FromBody] ForgotPasswordRequestDto request)
        => Contract("Recommended", "Send a password reset token/link.", nameof(ForgotPasswordRequestDto), nameof(MessageResponseDto), "Anonymous", "Email is required.", "Email must be valid.");

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public IActionResult ResetPassword([FromBody] ResetPasswordRequestDto request)
        => Contract("Recommended", "Reset password using a valid reset token.", nameof(ResetPasswordRequestDto), nameof(MessageResponseDto), "Anonymous", "Email and token are required.", "New password must satisfy password policy.", "Confirm password must match.");
}

public sealed record RefreshTokenRequestDto(string RefreshToken);
public sealed record LogoutRequestDto(string RefreshToken);
public sealed record ChangePasswordRequestDto(string CurrentPassword, string NewPassword, string ConfirmPassword);
public sealed record ForgotPasswordRequestDto(string Email);
public sealed record ResetPasswordRequestDto(string Email, string Token, string NewPassword, string ConfirmPassword);

[ApiController]
[Route("api/v1/employees")]
[Authorize]
public sealed class EmployeesController : ContractControllerBase
{
    [HttpPost]
    [Authorize(Roles = Roles.HrOrAdmin)]
    public IActionResult Create([FromBody] CreateEmployeeRequestDto request)
        => Contract("MVP", "Create employee master data and optionally link a user account.", nameof(CreateEmployeeRequestDto), nameof(EmployeeDetailsResponseDto), "HR, Admin", "Employee code, full name, email, department, work schedule, office location, and hire date are required.", "Employee code and email must be unique.");

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.HrOrAdmin)]
    public IActionResult Update(Guid id, [FromBody] UpdateEmployeeRequestDto request)
        => Contract("MVP", "Update employee master data.", nameof(UpdateEmployeeRequestDto), nameof(EmployeeDetailsResponseDto), "HR, Admin", "Employee must exist.", "Email must remain unique.");

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.HrOrAdmin)]
    public IActionResult Delete(Guid id)
        => Contract("MVP", "Deactivate an employee.", "None", nameof(MessageResponseDto), "HR, Admin", "Employee must exist.", "Use soft delete to preserve attendance history.");

    [HttpGet("{id:guid}")]
    public IActionResult Details(Guid id)
        => Contract("MVP", "Get employee details.", "None", nameof(EmployeeDetailsResponseDto), "HR, Admin, Manager, Self", "Employee must exist.", "Managers may only access employees in scope.");

    [HttpGet]
    [Authorize(Roles = Roles.ManagerHrOrAdmin)]
    public IActionResult List([FromQuery] EmployeeQueryDto query)
        => Contract("MVP", "Get paged employee list.", nameof(EmployeeQueryDto), "PagedResponse<EmployeeListItemResponseDto>", "Manager, HR, Admin", "Page number and page size must be valid.");

    [HttpGet("search")]
    public IActionResult Search([FromQuery] EmployeeSearchQueryDto query)
        => Contract("Recommended", "Search employees by name, code, email, or department.", nameof(EmployeeSearchQueryDto), "IReadOnlyList<EmployeeListItemResponseDto>", "Authenticated", "Search term must meet minimum length.", "Limit result count.");

    [HttpGet("{id:guid}/attendance-summary")]
    public IActionResult AttendanceSummary(Guid id, [FromQuery] DateRangeQueryDto query)
        => Contract("Recommended", "Get attendance summary for one employee.", nameof(DateRangeQueryDto), nameof(EmployeeAttendanceSummaryResponseDto), "HR, Admin, Manager, Self", "Date range is required.", "Date range must be within reporting limit.");
}

public sealed record CreateEmployeeRequestDto(string EmployeeCode, string FullName, string Email, string? Phone, Guid DepartmentId, Guid WorkScheduleId, Guid OfficeLocationId, DateOnly HireDate, string ContractType, string? UserId);
public sealed record UpdateEmployeeRequestDto(string FullName, string Email, string? Phone, Guid DepartmentId, Guid WorkScheduleId, Guid OfficeLocationId, DateOnly? DateOfBirth, bool IsActive);
public sealed record EmployeeQueryDto(int PageNumber = 1, int PageSize = 20, Guid? DepartmentId = null, bool? IsActive = null);
public sealed record EmployeeSearchQueryDto(string Query, int Limit = 10);
public sealed record EmployeeDetailsResponseDto(Guid Id, string EmployeeCode, string FullName, string Email, string? Phone, Guid DepartmentId, string DepartmentName, Guid WorkScheduleId, Guid OfficeLocationId, DateOnly HireDate, bool IsActive);
public sealed record EmployeeListItemResponseDto(Guid Id, string EmployeeCode, string FullName, string Email, string DepartmentName, bool IsActive);
public sealed record EmployeeAttendanceSummaryResponseDto(Guid EmployeeId, int PresentDays, int AbsentDays, int LateDays, decimal OvertimeHours);

[ApiController]
[Route("api/v1/departments")]
[Authorize]
public sealed class DepartmentsController : ContractControllerBase
{
    [HttpPost]
    [Authorize(Roles = Roles.HrOrAdmin)]
    public IActionResult Create([FromBody] CreateDepartmentRequestDto request)
        => Contract("MVP", "Create department.", nameof(CreateDepartmentRequestDto), nameof(DepartmentResponseDto), "HR, Admin", "Name is required.", "Name must be unique.");

    [HttpGet]
    public IActionResult List([FromQuery] DepartmentQueryDto query)
        => Contract("MVP", "List departments.", nameof(DepartmentQueryDto), "PagedResponse<DepartmentResponseDto>", "Authenticated", "Page number and page size must be valid.");

    [HttpGet("{id:guid}")]
    public IActionResult Details(Guid id)
        => Contract("MVP", "Get department details.", "None", nameof(DepartmentResponseDto), "Authenticated", "Department must exist.");

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.HrOrAdmin)]
    public IActionResult Update(Guid id, [FromBody] UpdateDepartmentRequestDto request)
        => Contract("MVP", "Update department.", nameof(UpdateDepartmentRequestDto), nameof(DepartmentResponseDto), "HR, Admin", "Department must exist.", "Name must be unique.");

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public IActionResult Delete(Guid id)
        => Contract("MVP", "Deactivate department.", "None", nameof(MessageResponseDto), "Admin", "Department must exist.", "Department must have no active employees or a reassignment plan.");
}

public sealed record CreateDepartmentRequestDto(string Name, Guid? ParentDepartmentId, Guid? HeadEmployeeId, Guid? DefaultWorkScheduleId);
public sealed record UpdateDepartmentRequestDto(string Name, Guid? HeadEmployeeId, Guid? DefaultWorkScheduleId, bool IsActive);
public sealed record DepartmentQueryDto(int PageNumber = 1, int PageSize = 20, bool? IsActive = null);
public sealed record DepartmentResponseDto(Guid Id, string Name, Guid? ParentDepartmentId, Guid? HeadEmployeeId, int EmployeeCount, bool IsActive);

[ApiController]
[Route("api/v1/attendance")]
[Authorize]
public sealed class AttendanceV1Controller : ContractControllerBase
{
    [HttpPost("check-in")]
    public IActionResult CheckIn([FromBody] CheckInRequestDto request)
        => Contract("MVP", "Record employee check-in with optional GPS/device evidence.", nameof(CheckInRequestDto), nameof(AttendanceRecordResponseDto), "Employee", "Employee must not already be checked in.", "GPS must be valid when GPS is required.", "Photo is required when configured.");

    [HttpPost("check-out")]
    public IActionResult CheckOut([FromBody] CheckOutRequestDto request)
        => Contract("MVP", "Record employee check-out.", nameof(CheckOutRequestDto), nameof(AttendanceRecordResponseDto), "Employee", "Employee must have an open check-in.", "GPS must be valid when GPS is required.");

    [HttpGet("today")]
    public IActionResult Today()
        => Contract("MVP", "Get current user's attendance for today.", "None", nameof(TodayAttendanceResponseDto), "Employee", "Authenticated employee profile is required.");

    [HttpGet("history")]
    public IActionResult History([FromQuery] DateRangeQueryDto query)
        => Contract("MVP", "Get current user's attendance history.", nameof(DateRangeQueryDto), "PagedResponse<AttendanceRecordResponseDto>", "Employee", "Date range must be valid.", "Page number and page size must be valid.");

    [HttpGet("statistics")]
    public IActionResult Statistics([FromQuery] DateRangeQueryDto query)
        => Contract("Recommended", "Get attendance statistics for current user or authorized scope.", nameof(DateRangeQueryDto), nameof(AttendanceStatisticsResponseDto), "Employee, Manager, HR, Admin", "Date range must be valid.");

    [HttpGet("monthly-statistics")]
    public IActionResult MonthlyStatistics([FromQuery] MonthlyStatisticsQueryDto query)
        => Contract("Recommended", "Get monthly attendance statistics.", nameof(MonthlyStatisticsQueryDto), nameof(MonthlyAttendanceStatisticsResponseDto), "Employee, Manager, HR, Admin", "Month must be 1-12.", "Year must be valid.");

    [HttpGet("employees/{employeeId:guid}/records")]
    [Authorize(Roles = Roles.ManagerHrOrAdmin)]
    public IActionResult EmployeeRecords(Guid employeeId, [FromQuery] DateRangeQueryDto query)
        => Contract("Recommended", "Get attendance records for one employee.", nameof(DateRangeQueryDto), "PagedResponse<AttendanceRecordResponseDto>", "Manager, HR, Admin", "Employee must exist.", "Manager must be in approval scope.");

    [HttpPost("corrections")]
    public IActionResult CreateCorrection([FromBody] CreateAttendanceCorrectionRequestDto request)
        => Contract("Recommended", "Request correction for an attendance record.", nameof(CreateAttendanceCorrectionRequestDto), nameof(AttendanceCorrectionResponseDto), "Employee", "Attendance record must exist.", "Reason is required.", "Requested times must be logical.");

    [HttpGet("corrections")]
    [Authorize(Roles = Roles.ManagerHrOrAdmin)]
    public IActionResult Corrections([FromQuery] CorrectionQueryDto query)
        => Contract("Enterprise", "List attendance correction requests.", nameof(CorrectionQueryDto), "PagedResponse<AttendanceCorrectionResponseDto>", "Manager, HR, Admin", "Filters must be valid.");

    [HttpPost("corrections/{id:guid}/approve")]
    [Authorize(Roles = Roles.ManagerHrOrAdmin)]
    public IActionResult ApproveCorrection(Guid id, [FromBody] ApprovalRequestDto request)
        => Contract("Enterprise", "Approve an attendance correction request.", nameof(ApprovalRequestDto), nameof(AttendanceCorrectionResponseDto), "Manager, HR, Admin", "Request must be pending.", "Approver must be authorized.");

    [HttpPost("corrections/{id:guid}/reject")]
    [Authorize(Roles = Roles.ManagerHrOrAdmin)]
    public IActionResult RejectCorrection(Guid id, [FromBody] RejectionRequestDto request)
        => Contract("Enterprise", "Reject an attendance correction request.", nameof(RejectionRequestDto), nameof(AttendanceCorrectionResponseDto), "Manager, HR, Admin", "Request must be pending.", "Reason is required.");

    [HttpGet("approvals/pending")]
    [Authorize(Roles = Roles.ManagerHrOrAdmin)]
    public IActionResult PendingApprovals([FromQuery] ApprovalQueryDto query)
        => Contract("Enterprise", "List pending attendance approvals.", nameof(ApprovalQueryDto), "PagedResponse<AttendanceApprovalResponseDto>", "Manager, HR, Admin", "Filters must be valid.");

    [HttpGet("current-status")]
    public IActionResult CurrentStatus()
        => Contract("MVP", "Get current check-in/check-out status.", "None", nameof(CurrentAttendanceStatusResponseDto), "Employee", "Authenticated employee profile is required.");
}

public sealed record CheckInRequestDto(double? Latitude, double? Longitude, double? AccuracyMeters, string? DeviceId, string? IpAddress, string? PhotoBase64, string? Notes);
public sealed record CheckOutRequestDto(double? Latitude, double? Longitude, double? AccuracyMeters, string? DeviceId, string? IpAddress, string? PhotoBase64, string? Notes);
public sealed record MonthlyStatisticsQueryDto(int Month, int Year, Guid? EmployeeId, Guid? DepartmentId);
public sealed record CreateAttendanceCorrectionRequestDto(Guid AttendanceRecordId, DateTime? RequestedCheckInTime, DateTime? RequestedCheckOutTime, string Reason);
public sealed record CorrectionQueryDto(string? Status, Guid? EmployeeId, DateOnly? From, DateOnly? To, int PageNumber = 1, int PageSize = 20);
public sealed record ApprovalQueryDto(Guid? DepartmentId, int PageNumber = 1, int PageSize = 20);
public sealed record AttendanceRecordResponseDto(Guid Id, Guid EmployeeId, DateOnly Date, DateTime? CheckInTime, DateTime? CheckOutTime, string Status, decimal LateMinutes, decimal OvertimeHours);
public sealed record TodayAttendanceResponseDto(bool HasCheckedIn, bool HasCheckedOut, DateTime? CheckInTime, DateTime? CheckOutTime, string Status);
public sealed record AttendanceStatisticsResponseDto(int PresentDays, int AbsentDays, int LateDays, int LeaveDays, decimal OvertimeHours);
public sealed record MonthlyAttendanceStatisticsResponseDto(int Month, int Year, AttendanceStatisticsResponseDto Totals);
public sealed record AttendanceCorrectionResponseDto(Guid Id, Guid AttendanceRecordId, string Status, string Reason, DateTime CreatedAt);
public sealed record AttendanceApprovalResponseDto(Guid Id, Guid EmployeeId, string EmployeeName, string ApprovalType, string Status, DateTime CreatedAt);
public sealed record CurrentAttendanceStatusResponseDto(bool CanCheckIn, bool CanCheckOut, Guid? OpenAttendanceRecordId, DateTime? CheckInTime);

[ApiController]
[Route("api/v1/gps")]
[Authorize]
public sealed class GpsController : ContractControllerBase
{
    [HttpPost("validate-location")]
    public IActionResult ValidateLocation([FromBody] ValidateLocationRequestDto request)
        => Contract("MVP", "Validate a GPS coordinate for attendance.", nameof(ValidateLocationRequestDto), nameof(ValidateLocationResponseDto), "Employee", "Latitude and longitude are required.", "Accuracy must be within allowed threshold when supplied.");

    [HttpPost("verify-office-radius")]
    public IActionResult VerifyOfficeRadius([FromBody] OfficeRadiusVerificationRequestDto request)
        => Contract("MVP", "Verify GPS coordinate against configured office radius.", nameof(OfficeRadiusVerificationRequestDto), nameof(OfficeRadiusVerificationResponseDto), "Employee", "Office location must exist.", "Coordinates are required.");

    [HttpGet("offices")]
    public IActionResult Offices()
        => Contract("Enterprise", "List active office locations/geofences.", "None", "IReadOnlyList<OfficeLocationResponseDto>", "Authenticated", "Only active office locations are returned.");

    [HttpPost("geofence-events")]
    public IActionResult CreateGeofenceEvent([FromBody] CreateGeofenceEventRequestDto request)
        => Contract("Enterprise", "Log a geofence audit event.", nameof(CreateGeofenceEventRequestDto), nameof(GeofenceEventResponseDto), "Employee", "Event type and coordinates are required.");
}

public sealed record ValidateLocationRequestDto(double Latitude, double Longitude, double? AccuracyMeters);
public sealed record ValidateLocationResponseDto(bool IsValid, bool IsWithinAllowedRadius, double DistanceMeters, string? NearestOfficeName, string? Message);
public sealed record OfficeRadiusVerificationRequestDto(Guid? OfficeLocationId, double Latitude, double Longitude);
public sealed record OfficeRadiusVerificationResponseDto(bool IsInsideRadius, double DistanceMeters, double AllowedRadiusMeters);
public sealed record OfficeLocationResponseDto(Guid Id, string Name, double Latitude, double Longitude, double RadiusMeters, bool IsActive);
public sealed record CreateGeofenceEventRequestDto(string EventType, double Latitude, double Longitude, double? AccuracyMeters, string? DeviceId);
public sealed record GeofenceEventResponseDto(Guid Id, string EventType, DateTime CreatedAt);

[ApiController]
[Route("api/v1/dashboard")]
[Authorize(Roles = Roles.ManagerHrOrAdmin)]
public sealed class DashboardController : ContractControllerBase
{
    [HttpGet("summary")]
    public IActionResult Summary([FromQuery] DashboardQueryDto query)
        => Contract("MVP", "Get dashboard summary.", nameof(DashboardQueryDto), nameof(DashboardSummaryResponseDto), "Manager, HR, Admin", "Date must be valid.");

    [HttpGet("recent-activities")]
    public IActionResult RecentActivities([FromQuery] RecentActivityQueryDto query)
        => Contract("Recommended", "Get recent activity feed.", nameof(RecentActivityQueryDto), "PagedResponse<RecentActivityResponseDto>", "Manager, HR, Admin", "Page number and page size must be valid.");

    [HttpGet("attendance-trends")]
    public IActionResult AttendanceTrends([FromQuery] DateRangeQueryDto query)
        => Contract("Recommended", "Get attendance trend chart data.", nameof(DateRangeQueryDto), nameof(AttendanceTrendResponseDto), "Manager, HR, Admin", "Date range must be valid.");

    [HttpGet("departments/statistics")]
    public IActionResult DepartmentStatistics([FromQuery] DateRangeQueryDto query)
        => Contract("Recommended", "Get department-level attendance statistics.", nameof(DateRangeQueryDto), "IReadOnlyList<DepartmentAttendanceStatsResponseDto>", "HR, Admin", "Date range must be valid.");

    [HttpGet("employees/statistics")]
    public IActionResult EmployeeStatistics([FromQuery] EmployeeStatsQueryDto query)
        => Contract("Recommended", "Get employee-level attendance statistics.", nameof(EmployeeStatsQueryDto), "PagedResponse<EmployeeAttendanceStatsResponseDto>", "Manager, HR, Admin", "Filters must be valid.");
}

public sealed record DashboardQueryDto(DateOnly? Date, Guid? DepartmentId);
public sealed record RecentActivityQueryDto(int PageNumber = 1, int PageSize = 20);
public sealed record EmployeeStatsQueryDto(Guid? DepartmentId, DateOnly? From, DateOnly? To, int PageNumber = 1, int PageSize = 20);
public sealed record DashboardSummaryResponseDto(int TotalEmployees, int PresentToday, int AbsentToday, int LateToday, int OnLeaveToday, int PendingApprovals);
public sealed record RecentActivityResponseDto(Guid Id, string Type, string Title, string Description, DateTime CreatedAt);
public sealed record AttendanceTrendResponseDto(IReadOnlyList<string> Labels, IReadOnlyList<int> PresentCounts, IReadOnlyList<int> AbsentCounts, IReadOnlyList<int> LateCounts);
public sealed record DepartmentAttendanceStatsResponseDto(Guid DepartmentId, string DepartmentName, int Present, int Absent, int Late);
public sealed record EmployeeAttendanceStatsResponseDto(Guid EmployeeId, string EmployeeName, int Present, int Absent, int Late, decimal OvertimeHours);

[ApiController]
[Route("api/v1/leaves")]
[Authorize]
public sealed class LeavesV1Controller : ContractControllerBase
{
    [HttpPost]
    public IActionResult Create([FromBody] CreateLeaveRequestDto request)
        => Contract("MVP", "Create leave request.", nameof(CreateLeaveRequestDto), nameof(LeaveRequestResponseDto), "Employee", "Leave type is required.", "Start date must be before or equal to end date.", "Reason is required.");

    [HttpGet("history")]
    public IActionResult History([FromQuery] DateRangeQueryDto query)
        => Contract("MVP", "Get current user's leave history.", nameof(DateRangeQueryDto), "PagedResponse<LeaveRequestResponseDto>", "Employee", "Date range must be valid.");

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = Roles.ManagerHrOrAdmin)]
    public IActionResult Approve(Guid id, [FromBody] ApprovalRequestDto request)
        => Contract("MVP", "Approve leave request.", nameof(ApprovalRequestDto), nameof(LeaveRequestResponseDto), "Manager, HR, Admin", "Leave request must be pending.", "Approver must be authorized.");

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = Roles.ManagerHrOrAdmin)]
    public IActionResult Reject(Guid id, [FromBody] RejectionRequestDto request)
        => Contract("MVP", "Reject leave request.", nameof(RejectionRequestDto), nameof(LeaveRequestResponseDto), "Manager, HR, Admin", "Leave request must be pending.", "Reason is required.");

    [HttpGet]
    [Authorize(Roles = Roles.ManagerHrOrAdmin)]
    public IActionResult List([FromQuery] LeaveQueryDto query)
        => Contract("Recommended", "List leave requests.", nameof(LeaveQueryDto), "PagedResponse<LeaveRequestResponseDto>", "Manager, HR, Admin", "Filters must be valid.");

    [HttpGet("statistics")]
    public IActionResult Statistics([FromQuery] DateRangeQueryDto query)
        => Contract("Recommended", "Get leave statistics.", nameof(DateRangeQueryDto), nameof(LeaveStatisticsResponseDto), "Employee, Manager, HR, Admin", "Date range must be valid.");
}

public sealed record CreateLeaveRequestDto(Guid LeaveTypeId, DateOnly StartDate, DateOnly EndDate, bool IsHalfDay, string Reason);
public sealed record LeaveQueryDto(string? Status, Guid? EmployeeId, Guid? LeaveTypeId, int PageNumber = 1, int PageSize = 20);
public sealed record LeaveRequestResponseDto(Guid Id, Guid EmployeeId, string LeaveType, DateOnly StartDate, DateOnly EndDate, decimal TotalDays, string Status, string Reason);
public sealed record LeaveStatisticsResponseDto(decimal TotalAllocated, decimal Used, decimal Remaining, int PendingRequests);

[ApiController]
[Route("api/v1/reports")]
[Authorize(Roles = Roles.ManagerHrOrAdmin)]
public sealed class ReportsController : ContractControllerBase
{
    [HttpGet("attendance")]
    public IActionResult Attendance([FromQuery] FileExportQueryDto query)
        => Contract("MVP", "Generate attendance report.", nameof(FileExportQueryDto), "PagedResponse<AttendanceReportRowDto>", "Manager, HR, Admin", "Date range is required.", "Reporting scope must be authorized.");

    [HttpGet("employees")]
    [Authorize(Roles = Roles.HrOrAdmin)]
    public IActionResult Employees([FromQuery] EmployeeReportQueryDto query)
        => Contract("Recommended", "Generate employee report.", nameof(EmployeeReportQueryDto), "PagedResponse<EmployeeReportRowDto>", "HR, Admin", "Filters must be valid.");

    [HttpGet("departments")]
    [Authorize(Roles = Roles.HrOrAdmin)]
    public IActionResult Departments([FromQuery] DepartmentReportQueryDto query)
        => Contract("Recommended", "Generate department report.", nameof(DepartmentReportQueryDto), "PagedResponse<DepartmentReportRowDto>", "HR, Admin", "Filters must be valid.");

    [HttpGet("attendance/export/excel")]
    public IActionResult ExportAttendanceExcel([FromQuery] FileExportQueryDto query)
        => Contract("Recommended", "Export attendance report as Excel.", nameof(FileExportQueryDto), "FileResult", "Manager, HR, Admin", "Date range must be within export limit.");

    [HttpGet("attendance/export/pdf")]
    public IActionResult ExportAttendancePdf([FromQuery] FileExportQueryDto query)
        => Contract("Recommended", "Export attendance report as PDF.", nameof(FileExportQueryDto), "FileResult", "Manager, HR, Admin", "Date range must be within export limit.");

    [HttpPost("scheduled")]
    [Authorize(Roles = Roles.HrOrAdmin)]
    public IActionResult CreateScheduled([FromBody] CreateScheduledReportRequestDto request)
        => Contract("Enterprise", "Create scheduled report delivery.", nameof(CreateScheduledReportRequestDto), nameof(ScheduledReportResponseDto), "HR, Admin", "Frequency, report type, and recipients are required.");

    [HttpGet("audit-log")]
    [Authorize(Roles = Roles.AdminOrSuperAdmin)]
    public IActionResult AuditLog([FromQuery] DateRangeQueryDto query)
        => Contract("Enterprise", "Generate audit log report.", nameof(DateRangeQueryDto), "PagedResponse<AuditLogResponseDto>", "Admin, SuperAdmin", "Date range must be valid.");
}

public sealed record EmployeeReportQueryDto(Guid? DepartmentId, bool? IsActive, int PageNumber = 1, int PageSize = 20);
public sealed record DepartmentReportQueryDto(bool? IsActive, int PageNumber = 1, int PageSize = 20);
public sealed record CreateScheduledReportRequestDto(string ReportType, string Frequency, IReadOnlyList<string> Recipients, FileExportQueryDto Parameters);
public sealed record ScheduledReportResponseDto(Guid Id, string ReportType, string Frequency, bool IsActive);
public sealed record AuditLogResponseDto(Guid Id, string Actor, string Action, string EntityName, DateTime CreatedAt);

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public sealed class NotificationsController : ContractControllerBase
{
    [HttpGet]
    public IActionResult List([FromQuery] NotificationQueryDto query)
        => Contract("MVP", "Get current user's notifications.", nameof(NotificationQueryDto), "PagedResponse<NotificationResponseDto>", "Authenticated", "Page number and page size must be valid.");

    [HttpPost("{id:guid}/read")]
    public IActionResult MarkAsRead(Guid id)
        => Contract("MVP", "Mark one notification as read.", "None", nameof(MessageResponseDto), "Authenticated", "Notification must belong to current user.");

    [HttpGet("unread-count")]
    public IActionResult UnreadCount()
        => Contract("MVP", "Get current user's unread notification count.", "None", nameof(UnreadNotificationCountResponseDto), "Authenticated", "Authenticated user is required.");

    [HttpPost("read-all")]
    public IActionResult ReadAll()
        => Contract("Recommended", "Mark all current user's notifications as read.", "None", nameof(MessageResponseDto), "Authenticated", "Authenticated user is required.");

    [HttpPost("broadcast")]
    [Authorize(Roles = Roles.HrOrAdmin)]
    public IActionResult Broadcast([FromBody] BroadcastNotificationRequestDto request)
        => Contract("Enterprise", "Broadcast notification to a target audience.", nameof(BroadcastNotificationRequestDto), nameof(MessageResponseDto), "HR, Admin", "Title, message, and audience are required.");
}

public sealed record NotificationQueryDto(bool? IsRead, int PageNumber = 1, int PageSize = 20);
public sealed record BroadcastNotificationRequestDto(string Title, string Message, string Audience, IReadOnlyList<Guid>? EmployeeIds);
public sealed record NotificationResponseDto(Guid Id, string Title, string Message, string Type, bool IsRead, DateTime CreatedAt);
public sealed record UnreadNotificationCountResponseDto(int Count);

[ApiController]
[Route("api/v1/work-schedules")]
[Authorize(Roles = Roles.ManagerHrOrAdmin)]
public sealed class WorkSchedulesController : ContractControllerBase
{
    [HttpPost]
    [Authorize(Roles = Roles.HrOrAdmin)]
    public IActionResult Create([FromBody] CreateWorkScheduleRequestDto request)
        => Contract("Recommended", "Create work schedule.", nameof(CreateWorkScheduleRequestDto), nameof(WorkScheduleResponseDto), "HR, Admin", "Name, start time, end time, and working days are required.");

    [HttpGet]
    public IActionResult List([FromQuery] WorkScheduleQueryDto query)
        => Contract("Recommended", "List work schedules.", nameof(WorkScheduleQueryDto), "PagedResponse<WorkScheduleResponseDto>", "Manager, HR, Admin", "Page number and page size must be valid.");

    [HttpGet("{id:guid}")]
    public IActionResult Details(Guid id)
        => Contract("Recommended", "Get work schedule details.", "None", nameof(WorkScheduleResponseDto), "Manager, HR, Admin", "Schedule must exist.");

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.HrOrAdmin)]
    public IActionResult Update(Guid id, [FromBody] UpdateWorkScheduleRequestDto request)
        => Contract("Recommended", "Update work schedule.", nameof(UpdateWorkScheduleRequestDto), nameof(WorkScheduleResponseDto), "HR, Admin", "Schedule must exist.", "Working hours must be valid.");

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.HrOrAdmin)]
    public IActionResult Delete(Guid id)
        => Contract("Recommended", "Deactivate work schedule.", "None", nameof(MessageResponseDto), "HR, Admin", "Schedule must exist.", "Assigned employees require reassignment or policy override.");

    [HttpPost("{id:guid}/assign")]
    [Authorize(Roles = Roles.HrOrAdmin)]
    public IActionResult Assign(Guid id, [FromBody] AssignWorkScheduleRequestDto request)
        => Contract("Recommended", "Assign schedule to employees.", nameof(AssignWorkScheduleRequestDto), nameof(MessageResponseDto), "HR, Admin", "Employees must exist.", "Effective date is required.");

    [HttpGet("statistics")]
    public IActionResult Statistics([FromQuery] DateRangeQueryDto query)
        => Contract("Enterprise", "Get shift/schedule statistics.", nameof(DateRangeQueryDto), nameof(WorkScheduleStatisticsResponseDto), "HR, Admin", "Date range must be valid.");

    [HttpPost("bulk-assign")]
    [Authorize(Roles = Roles.HrOrAdmin)]
    public IActionResult BulkAssign([FromBody] AssignWorkScheduleRequestDto request)
        => Contract("Enterprise", "Bulk assign schedules.", nameof(AssignWorkScheduleRequestDto), nameof(BulkOperationResultDto), "HR, Admin", "Employee IDs and schedule ID are required.");
}

public sealed record CreateWorkScheduleRequestDto(string Name, TimeOnly StartTime, TimeOnly EndTime, TimeSpan GracePeriod, IReadOnlyList<DayOfWeek> WorkingDays, bool IsFlexible);
public sealed record UpdateWorkScheduleRequestDto(string Name, TimeOnly StartTime, TimeOnly EndTime, TimeSpan GracePeriod, IReadOnlyList<DayOfWeek> WorkingDays, bool IsFlexible, bool IsActive);
public sealed record AssignWorkScheduleRequestDto(Guid WorkScheduleId, IReadOnlyList<Guid> EmployeeIds, DateOnly EffectiveFrom, DateOnly? EffectiveTo);
public sealed record WorkScheduleQueryDto(bool? IsActive, int PageNumber = 1, int PageSize = 20);
public sealed record WorkScheduleResponseDto(Guid Id, string Name, TimeOnly StartTime, TimeOnly EndTime, TimeSpan GracePeriod, IReadOnlyList<DayOfWeek> WorkingDays, bool IsFlexible, bool IsActive);
public sealed record WorkScheduleStatisticsResponseDto(int AssignedEmployees, int ActiveSchedules, int FlexibleSchedules);
public sealed record BulkOperationResultDto(int Requested, int Succeeded, int Failed, IReadOnlyList<string> Errors);

[ApiController]
[Route("api/v1/holidays")]
[Authorize]
public sealed class HolidaysController : ContractControllerBase
{
    [HttpPost]
    [Authorize(Roles = Roles.HrOrAdmin)]
    public IActionResult Create([FromBody] CreateHolidayRequestDto request)
        => Contract("Recommended", "Create holiday.", nameof(CreateHolidayRequestDto), nameof(HolidayResponseDto), "HR, Admin", "Name and date are required.");

    [HttpGet]
    public IActionResult List([FromQuery] HolidayQueryDto query)
        => Contract("Recommended", "List holidays.", nameof(HolidayQueryDto), "PagedResponse<HolidayResponseDto>", "Authenticated", "Year/date filters must be valid.");

    [HttpGet("{id:guid}")]
    public IActionResult Details(Guid id)
        => Contract("Recommended", "Get holiday details.", "None", nameof(HolidayResponseDto), "Authenticated", "Holiday must exist.");

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.HrOrAdmin)]
    public IActionResult Update(Guid id, [FromBody] UpdateHolidayRequestDto request)
        => Contract("Recommended", "Update holiday.", nameof(UpdateHolidayRequestDto), nameof(HolidayResponseDto), "HR, Admin", "Holiday must exist.", "Date must be valid.");

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.HrOrAdmin)]
    public IActionResult Delete(Guid id)
        => Contract("Recommended", "Delete holiday.", "None", nameof(MessageResponseDto), "HR, Admin", "Holiday must exist.");

    [HttpPost("import")]
    [Authorize(Roles = Roles.HrOrAdmin)]
    public IActionResult Import([FromForm] ImportHolidayRequestDto request)
        => Contract("Enterprise", "Import holidays from a file.", nameof(ImportHolidayRequestDto), nameof(BulkOperationResultDto), "HR, Admin", "File and year are required.", "File type must be allowed.");
}

public sealed record CreateHolidayRequestDto(string Name, DateOnly Date, bool IsRecurring, string? Description);
public sealed record UpdateHolidayRequestDto(string Name, DateOnly Date, bool IsRecurring, string? Description);
public sealed record HolidayQueryDto(int? Year, DateOnly? From, DateOnly? To, int PageNumber = 1, int PageSize = 20);
public sealed record ImportHolidayRequestDto(IFormFile File, int Year);
public sealed record HolidayResponseDto(Guid Id, string Name, DateOnly Date, bool IsRecurring, string? Description);

[ApiController]
[Route("api/v1/profile")]
[Authorize]
public sealed class ProfileController : ContractControllerBase
{
    [HttpGet("me")]
    public IActionResult Me()
        => Contract("MVP", "Get current user profile.", "None", nameof(UserProfileResponseDto), "Authenticated", "Authenticated user is required.");

    [HttpPut("me")]
    public IActionResult Update([FromBody] UpdateProfileRequestDto request)
        => Contract("MVP", "Update current user profile.", nameof(UpdateProfileRequestDto), nameof(UserProfileResponseDto), "Authenticated", "Phone, language, and timezone must be valid when supplied.");

    [HttpPost("photo")]
    public IActionResult UploadPhoto([FromForm] UploadProfilePhotoRequestDto request)
        => Contract("Recommended", "Upload profile photo.", nameof(UploadProfilePhotoRequestDto), nameof(ProfilePhotoResponseDto), "Authenticated", "Image file is required.", "File size and content type must be allowed.");

    [HttpDelete("photo")]
    public IActionResult DeletePhoto()
        => Contract("Enterprise", "Remove profile photo.", "None", nameof(MessageResponseDto), "Authenticated", "Profile photo must exist.");
}

public sealed record UpdateProfileRequestDto(string? PhoneNumber, string? PreferredLanguage, string? TimeZone);
public sealed record UploadProfilePhotoRequestDto(IFormFile File);
public sealed record UserProfileResponseDto(Guid UserId, Guid EmployeeId, string FullName, string Email, string? PhoneNumber, string? ProfilePhotoUrl, IReadOnlyList<string> Roles);
public sealed record ProfilePhotoResponseDto(string Url);

[ApiController]
[Route("api/v1/settings")]
[Authorize(Roles = Roles.HrOrAdmin)]
public sealed class SettingsController : ContractControllerBase
{
    [HttpGet("company")]
    public IActionResult Company()
        => Contract("Recommended", "Get company settings.", "None", nameof(CompanySettingsResponseDto), "HR, Admin", "Settings must exist.");

    [HttpPut("company")]
    [Authorize(Roles = Roles.Admin)]
    public IActionResult UpdateCompany([FromBody] UpdateCompanySettingsRequestDto request)
        => Contract("Recommended", "Update company settings.", nameof(UpdateCompanySettingsRequestDto), nameof(CompanySettingsResponseDto), "Admin", "Company name and timezone are required.", "Formats must be valid.");

    [HttpGet("attendance-rules")]
    public IActionResult AttendanceRules()
        => Contract("MVP", "Get attendance rules.", "None", nameof(AttendanceRulesResponseDto), "HR, Admin", "Settings must exist.");

    [HttpPut("attendance-rules")]
    public IActionResult UpdateAttendanceRules([FromBody] UpdateAttendanceRulesRequestDto request)
        => Contract("MVP", "Update attendance rules.", nameof(UpdateAttendanceRulesRequestDto), nameof(AttendanceRulesResponseDto), "HR, Admin", "Grace period cannot be negative.", "Auto checkout time is required when auto checkout is enabled.");

    [HttpGet("gps")]
    public IActionResult Gps()
        => Contract("MVP", "Get GPS settings.", "None", nameof(GpsSettingsResponseDto), "HR, Admin", "Settings must exist.");

    [HttpPut("gps")]
    [Authorize(Roles = Roles.Admin)]
    public IActionResult UpdateGps([FromBody] UpdateGpsSettingsRequestDto request)
        => Contract("MVP", "Update GPS settings.", nameof(UpdateGpsSettingsRequestDto), nameof(GpsSettingsResponseDto), "Admin", "Allowed radius must be positive.", "Coordinates must be valid.");

    [HttpGet("security")]
    [Authorize(Roles = Roles.AdminOrSuperAdmin)]
    public IActionResult Security()
        => Contract("Enterprise", "Get security settings.", "None", nameof(SecuritySettingsResponseDto), "Admin, SuperAdmin", "Settings must exist.");

    [HttpPut("security")]
    [Authorize(Roles = Roles.SuperAdmin)]
    public IActionResult UpdateSecurity([FromBody] UpdateSecuritySettingsRequestDto request)
        => Contract("Enterprise", "Update security settings.", nameof(UpdateSecuritySettingsRequestDto), nameof(SecuritySettingsResponseDto), "SuperAdmin", "Password, lockout, and MFA policies must be valid.");
}

public sealed record UpdateCompanySettingsRequestDto(string CompanyName, string TimeZone, string DateFormat, string TimeFormat, string? LogoUrl);
public sealed record UpdateAttendanceRulesRequestDto(TimeSpan GracePeriod, bool RequireGpsForCheckIn, bool RequireGpsForCheckOut, bool AllowRemoteCheckIn, bool AutoCheckoutEnabled, TimeOnly? AutoCheckoutTime, bool OvertimeEnabled);
public sealed record UpdateGpsSettingsRequestDto(bool Enabled, double OfficeLatitude, double OfficeLongitude, double AllowedRadiusMeters, bool BlockOutsideRadius);
public sealed record UpdateSecuritySettingsRequestDto(bool MfaRequired, int MaxFailedLoginAttempts, int LockoutMinutes, int PasswordExpiryDays);
public sealed record CompanySettingsResponseDto(string CompanyName, string TimeZone, string DateFormat, string TimeFormat, string? LogoUrl);
public sealed record AttendanceRulesResponseDto(TimeSpan GracePeriod, bool RequireGpsForCheckIn, bool RequireGpsForCheckOut, bool AllowRemoteCheckIn, bool AutoCheckoutEnabled, TimeOnly? AutoCheckoutTime, bool OvertimeEnabled);
public sealed record GpsSettingsResponseDto(bool Enabled, double OfficeLatitude, double OfficeLongitude, double AllowedRadiusMeters, bool BlockOutsideRadius);
public sealed record SecuritySettingsResponseDto(bool MfaRequired, int MaxFailedLoginAttempts, int LockoutMinutes, int PasswordExpiryDays);
