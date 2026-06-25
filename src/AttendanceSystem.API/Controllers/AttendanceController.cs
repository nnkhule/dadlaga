using System.Security.Claims;
using AttendanceSystem.Application.Features.Attendance.Commands.CheckIn;
using AttendanceSystem.Application.Features.Attendance.Commands.CheckOut;
using AttendanceSystem.Application.Features.Attendance.Queries.GetAttendanceStatistics;
using AttendanceSystem.Application.Features.Attendance.Queries.GetTodayAttendance;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AttendanceSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AttendanceSystem.API.Controllers;

[ApiController]
[Route("api/attendance")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AttendanceController> _logger;

    public AttendanceController(IMediator mediator, ApplicationDbContext db, ILogger<AttendanceController> logger)
    {
        _mediator = mediator;
        _db = db;
        _logger = logger;
    }

    [HttpPost("checkin")]
    public async Task<IActionResult> CheckIn([FromBody] CheckInRequest request, CancellationToken cancellationToken )
    {
        var employeeId = GetEmployeeId();
        if (employeeId is null)
            return BadRequest(new ApiErrorResponse("Employee profile not linked to user."));

        var result = await _mediator.Send(new CheckInCommand(
            employeeId.Value,
            request.Latitude,
            request.Longitude,
            request.LocationPermissionGranted,
            request.PhotoBase64,
            request.VerificationMethod ?? "Gps"), cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Check-in failed for employee {EmployeeId}: {Error} ({ErrorCode})",
                employeeId, result.Error, result.ErrorCode);
            return BadRequest(new ApiErrorResponse(result.Error, result.ErrorCode));
        }

        return Ok(result.Value);
    }

    [HttpPost("checkout")]
    public async Task<IActionResult> CheckOut([FromBody] CheckOutRequest request, CancellationToken cancellationToken )
    {
        var employeeId = GetEmployeeId();
        if (employeeId is null)
            return BadRequest(new ApiErrorResponse("Employee profile not linked."));

        var result = await _mediator.Send(new CheckOutCommand(
            employeeId.Value,
            request.Latitude,
            request.Longitude,
            request.VerificationMethod ?? "Gps"), cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Check-out failed for employee {EmployeeId}: {Error} ({ErrorCode})",
                employeeId, result.Error, result.ErrorCode);
            return BadRequest(new ApiErrorResponse(result.Error, result.ErrorCode));
        }

        return Ok(result.Value);
    }

    [HttpGet("today")]
    public async Task<IActionResult> Today(CancellationToken cancellationToken)
    {
        var employeeId = GetEmployeeId();
        if (employeeId is null)
            return BadRequest();

        var result = await _mediator.Send(new GetTodayAttendanceQuery(employeeId.Value), cancellationToken);
        if (result.Value is null)
            return Ok(null);

        var a = result.Value;
        var statusStr = AttendanceStatusClassifier.ToDisplayName(a.Status);

        var workHours = a.CheckOutTime.HasValue
            ? Math.Round((decimal)(a.CheckOutTime.Value - a.CheckInTime).TotalHours, 2)
            : 0;

        var todayResponse = new TodayAttendanceApiDto(
            a.Id,
            a.EmployeeId,
            a.Date,
            a.CheckInTime,
            a.CheckOutTime,
            workHours,
            a.OvertimeHours,
            a.OvertimeHours,
            a.ShortHours,
            a.LateMinutes,
            a.VerificationMethod.ToString(),
            statusStr,
            statusStr,
            a.IsSuspicious,
            a.IsAutoGeo
        );

        return Ok(todayResponse);
    }

    [HttpGet("statistics")]
    public async Task<IActionResult> Statistics([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken )
    {
        var employeeId = GetEmployeeId();
        if (employeeId is null)
            return BadRequest(new ApiErrorResponse("Employee profile not linked to user."));

        var today = DateOnly.FromDateTime(DateTime.Now);
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? today;

        var records = await _db.AttendanceRecords
            .AsNoTracking()
            .Where(a => a.EmployeeId == employeeId.Value && a.Date >= start && a.Date <= end)
            .ToListAsync(cancellationToken);

        var presentDays = records.Count(a => AttendanceStatusClassifier.IsPresentBucket(a.Status));
        var absentDays = records.Count(a => AttendanceStatusClassifier.IsAbsent(a.Status));
        var lateDays = records.Count(a => AttendanceStatusClassifier.IsLate(a.Status));
        var leaveDays = records.Count(a => AttendanceStatusClassifier.IsOnLeave(a.Status));
        var overtimeHours = records.Sum(a => a.OvertimeHours);
        var shortHours = records.Sum(a => a.ShortHours);
        var totalMarkedDays = presentDays + lateDays + absentDays + leaveDays;
        var attendanceRate = totalMarkedDays == 0 ? 0 : Math.Round((decimal)(presentDays + lateDays) / totalMarkedDays * 100, 2);

        return Ok(new AttendanceStatisticsApiDto(presentDays, absentDays, lateDays, leaveDays, overtimeHours, shortHours, attendanceRate));
    }

    [HttpGet("history")]
    public async Task<IActionResult> History(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var employeeId = GetEmployeeId();
        if (employeeId is null)
            return BadRequest(new ApiErrorResponse("Employee profile not linked to user."));

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var start = from ?? today.AddDays(-30);
        var end = to ?? today;

        var query = _db.AttendanceRecords
            .AsNoTracking()
            .Include(a => a.Employee)
            .Where(a => a.EmployeeId == employeeId.Value && a.Date >= start && a.Date <= end)
            .OrderByDescending(a => a.Date)
            .ThenByDescending(a => a.CheckInTime);

        var total = await query.CountAsync(cancellationToken);

        var raw = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.EmployeeId,
                EmployeeName = a.Employee == null ? null : a.Employee.FullName,
                a.Date,
                a.CheckInTime,
                a.CheckOutTime,
                a.OvertimeHours,
                a.ShortHours,
                a.LateMinutes,
                VerificationMethod = a.VerificationMethod,
                a.Status
            })
            .ToListAsync(cancellationToken);

        var items = raw.Select(a =>
        {
            var statusStr = AttendanceStatusClassifier.ToDisplayName(a.Status);
            var workHours = a.CheckOutTime.HasValue
                ? Math.Round((decimal)(a.CheckOutTime.Value - a.CheckInTime).TotalHours, 2)
                : 0;

            return new AttendanceHistoryApiDto(
                a.Id, a.EmployeeId, a.EmployeeName, a.Date,
                a.CheckInTime, a.CheckOutTime,
                workHours, a.OvertimeHours, a.OvertimeHours, a.ShortHours, a.LateMinutes,
                a.VerificationMethod.ToString(),
                statusStr,
                statusStr);
        }).ToList();

        return Ok(new PagedResponseDto<AttendanceHistoryApiDto>(items, pageNumber, pageSize, total));
    }

    [HttpGet("records")]
    public async Task<IActionResult> Records(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] Guid? departmentId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var start = from ?? today;
        var end = to ?? today;

        var query = _db.AttendanceRecords
            .AsNoTracking()
            .Include(a => a.Employee)
            .ThenInclude(e => e!.Department)
            .Where(a => a.Date >= start && a.Date <= end);

        if (departmentId.HasValue)
            query = query.Where(a => a.Employee != null && a.Employee.DepartmentId == departmentId.Value);

        query = query.OrderByDescending(a => a.Date).ThenBy(a => a.Employee == null ? null : a.Employee.FullName);

        var total = await query.CountAsync(cancellationToken);

        var raw = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.Id,
                a.EmployeeId,
                EmployeeName = a.Employee == null ? null : a.Employee.FullName,
                a.Date,
                a.CheckInTime,
                a.CheckOutTime,
                a.OvertimeHours,
                a.ShortHours,
                a.LateMinutes,
                VerificationMethod = a.VerificationMethod,
                a.Status
            })
            .ToListAsync(cancellationToken);

        var items = raw.Select(a =>
        {
            var statusStr = AttendanceStatusClassifier.ToDisplayName(a.Status);
            var workHours = a.CheckOutTime.HasValue
                ? Math.Round((decimal)(a.CheckOutTime.Value - a.CheckInTime).TotalHours, 2)
                : 0;

            return new AttendanceHistoryApiDto(
                a.Id, a.EmployeeId, a.EmployeeName, a.Date,
                a.CheckInTime, a.CheckOutTime,
                workHours, a.OvertimeHours, a.ShortHours, a.OvertimeHours, a.LateMinutes,
                a.VerificationMethod.ToString(),
                statusStr,
                statusStr);
        }).ToList();

        return Ok(new PagedResponseDto<AttendanceHistoryApiDto>(items, pageNumber, pageSize, total));
    }

    [HttpGet("validate-location")]
    public async Task<ActionResult<LocationValidationApiDto>> ValidateLocationGet(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        CancellationToken cancellationToken)
        => await ValidateLocationCore(latitude, longitude, cancellationToken);

    [HttpPost("validate-location")]
    public async Task<ActionResult<LocationValidationApiDto>> ValidateLocationPost(
        [FromBody] LocationValidationRequestApiDto request,
        CancellationToken cancellationToken )
        => await ValidateLocationCore(request.Latitude, request.Longitude, cancellationToken);

    private Guid? GetEmployeeId()
    {
        var claim = User.FindFirstValue("employee_id");
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private sealed record ApiErrorResponse(string Message, string? Code = null);
    private sealed record NearestOfficeDistance(OfficeLocation Office, double Distance);
    private sealed record ClaimDebugDto(string Type, string Value);

    private async Task<ActionResult<LocationValidationApiDto>> ValidateLocationCore(
        double latitude, double longitude, CancellationToken cancellationToken )
    {
        var offices = await _db.OfficeLocations.AsNoTracking().Where(o => o.IsActive).ToListAsync(cancellationToken);
        var nearest = offices
            .Select(o => new NearestOfficeDistance(o, GetDistanceMeters(latitude, longitude, o.Latitude, o.Longitude)))
            .OrderBy(x => x.Distance)
            .FirstOrDefault();

        var isWithin = nearest is not null && nearest.Distance <= nearest.Office.RadiusMeters;
        return Ok(new LocationValidationApiDto(
            null,
            nearest?.Office.Name,
            nearest?.Distance ?? 0,
            nearest?.Distance ?? 0,
            isWithin ? "Inside" : "Outside",
            isWithin,
            isWithin,
            null));
    }

    private static double GetDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double radius = 6371000;
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return radius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}

public record CheckInRequest(
    double? Latitude, double? Longitude,
    bool LocationPermissionGranted = true,
    string? PhotoBase64 = null,
    string? VerificationMethod = "Gps");

public record CheckOutRequest(
    double? Latitude, double? Longitude,
    string? VerificationMethod = "Gps");

public sealed record AttendanceStatisticsApiDto(
    int PresentDays, int AbsentDays, int LateDays, int LeaveDays,
    decimal OvertimeHours, decimal ShortHours, decimal AttendanceRate);

public sealed record TodayAttendanceApiDto(
    Guid Id, Guid EmployeeId, DateOnly Date,
    DateTime? CheckInTime, DateTime? CheckOutTime,
    decimal WorkHours, decimal Overtime, decimal OvertimeHours, decimal ShortHours, decimal LateMinutes,
    string? VerificationMethod, string? AttendanceStatus, string? Status,
    bool IsSuspicious, bool IsAutoGeo);

public sealed record AttendanceHistoryApiDto(
    Guid Id, Guid? EmployeeId, string? EmployeeName, DateOnly Date,
    DateTime? CheckInTime, DateTime? CheckOutTime,
    decimal WorkHours, decimal Overtime, decimal OvertimeHours, decimal ShortHours, decimal LateMinutes,
    string? VerificationMethod, string? AttendanceStatus, string? Status);

public sealed record LocationValidationRequestApiDto(double Latitude, double Longitude);
public sealed record LocationValidationApiDto(
    string? CurrentLocation, string? OfficeLocation,
    double Distance, double DistanceMeters,
    string? ValidationStatus, bool IsValid, bool IsWithinAllowedRadius, string? Message);
