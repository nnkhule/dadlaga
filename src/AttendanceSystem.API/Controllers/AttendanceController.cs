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
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.API.Controllers;

/// <summary>
/// Attendance check-in/out and query endpoints.
/// </summary>
[ApiController]
[Route("api/attendance")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ApplicationDbContext _db;

    public AttendanceController(IMediator mediator, ApplicationDbContext db)
    {
        _mediator = mediator;
        _db = db;
    }

    /// <summary>Records employee check-in with optional GPS.</summary>
    [HttpPost("checkin")]
    public async Task<IActionResult> CheckIn([FromBody] CheckInRequest request, CancellationToken cancellationToken)
    {
        var employeeId = GetEmployeeId();
        if (employeeId is null)
            return BadRequest(new { message = "Employee profile not linked to user." });

        var result = await _mediator.Send(new CheckInCommand(
            employeeId.Value,
            request.Latitude,
            request.Longitude,
            request.LocationPermissionGranted,
            request.PhotoBase64,
            request.VerificationMethod ?? "Gps"), cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { message = result.Error, code = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>Records employee check-out.</summary>
    [HttpPost("checkout")]
    public async Task<IActionResult> CheckOut([FromBody] CheckOutRequest request, CancellationToken cancellationToken)
    {
        var employeeId = GetEmployeeId();
        if (employeeId is null)
            return BadRequest(new { message = "Employee profile not linked." });

        var result = await _mediator.Send(new CheckOutCommand(
            employeeId.Value,
            request.Latitude,
            request.Longitude,
            request.VerificationMethod ?? "Gps"), cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { message = result.Error, code = result.ErrorCode });

        return Ok(result.Value);
    }

    /// <summary>Returns today's attendance for the current employee.</summary>
    [HttpGet("today")]
    public async Task<IActionResult> Today(CancellationToken cancellationToken)
    {
        var employeeId = GetEmployeeId();
        if (employeeId is null)
            return BadRequest();

        var result = await _mediator.Send(new GetTodayAttendanceQuery(employeeId.Value), cancellationToken);
        return Ok(result.Value);
    }

    /// <summary>Returns attendance statistics for the current month.</summary>
    [HttpGet("statistics")]
    public async Task<IActionResult> Statistics([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken cancellationToken)
    {
        var employeeId = GetEmployeeId();
        if (employeeId is null)
            return BadRequest(new { message = "Employee profile not linked to user." });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? new DateOnly(today.Year, today.Month, 1);
        var end = to ?? today;

        var records = await _db.AttendanceRecords
            .AsNoTracking()
            .Where(a => a.EmployeeId == employeeId.Value && a.Date >= start && a.Date <= end)
            .ToListAsync(cancellationToken);

        var presentDays = records.Count(a => a.Status != AttendanceStatus.Absent && a.Status != AttendanceStatus.OnLeave);
        var absentDays = records.Count(a => a.Status == AttendanceStatus.Absent);
        var lateDays = records.Count(a => a.Status == AttendanceStatus.Late || a.LateMinutes > 0);
        var leaveDays = records.Count(a => a.Status == AttendanceStatus.OnLeave);
        var overtimeHours = records.Sum(a => a.OvertimeHours);
        var totalMarkedDays = presentDays + absentDays + leaveDays;
        var attendanceRate = totalMarkedDays == 0 ? 0 : Math.Round((decimal)presentDays / totalMarkedDays * 100, 2);

        return Ok(new AttendanceStatisticsApiDto(presentDays, absentDays, lateDays, leaveDays, overtimeHours, attendanceRate));
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
            return BadRequest(new { message = "Employee profile not linked to user." });

        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var start = from ?? today.AddDays(-30);
        var end = to ?? today;

        var query = _db.AttendanceRecords
            .AsNoTracking()
            .Include(a => a.Employee)
            .Where(a => a.EmployeeId == employeeId.Value && a.Date >= start && a.Date <= end)
            .OrderByDescending(a => a.Date)
            .ThenByDescending(a => a.CheckInTime);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AttendanceHistoryApiDto(
                a.Id,
                a.EmployeeId,
                a.Employee == null ? null : a.Employee.FullName,
                a.Date,
                a.CheckInTime,
                a.CheckOutTime,
                a.CheckOutTime == null ? 0 : Math.Round((decimal)(a.CheckOutTime.Value - a.CheckInTime).TotalHours, 2),
                a.OvertimeHours,
                a.OvertimeHours,
                a.LateMinutes,
                a.VerificationMethod.ToString(),
                a.Status.ToString(),
                a.Status.ToString()))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponseDto<AttendanceHistoryApiDto>(items, pageNumber, pageSize, total));
    }

    [HttpGet("debug")]
    public IActionResult Debug()
    {
        return Ok(User.Claims.Select(x => new
        {
            x.Type,
            x.Value
        }));
    }

 

    private Guid? GetEmployeeId()
    {
        var claim = User.FindFirstValue("employee_id");
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}

/// <summary>Check-in API request model.</summary>
public record CheckInRequest(
    double? Latitude,
    double? Longitude,
    bool LocationPermissionGranted = true,
    string? PhotoBase64 = null,
    string? VerificationMethod = "Gps");

/// <summary>Check-out API request model.</summary>
public record CheckOutRequest(
    double? Latitude,
    double? Longitude,
    string? VerificationMethod = "Gps");

public sealed record AttendanceStatisticsApiDto(
    int PresentDays,
    int AbsentDays,
    int LateDays,
    int LeaveDays,
    decimal OvertimeHours,
    decimal AttendanceRate);

public sealed record AttendanceHistoryApiDto(
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
