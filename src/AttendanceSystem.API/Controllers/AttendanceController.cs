using System.Security.Claims;
using AttendanceSystem.Application.Features.Attendance.Commands.CheckIn;
using AttendanceSystem.Application.Features.Attendance.Commands.CheckOut;
using AttendanceSystem.Application.Features.Attendance.Queries.GetAttendanceStatistics;
using AttendanceSystem.Application.Features.Attendance.Queries.GetTodayAttendance;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    public AttendanceController(IMediator mediator) => _mediator = mediator;

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
    public async Task<IActionResult> Statistics(CancellationToken cancellationToken)
    {
        var employeeId = GetEmployeeId();
        if (employeeId is null)
            return BadRequest(new { message = "Employee profile not linked to user." });

        var result = await _mediator.Send(new GetAttendanceStatisticsQuery(employeeId.Value), cancellationToken);
        if (!result.IsSuccess)
            return BadRequest(new { message = result.Error, code = result.ErrorCode });

        return Ok(result.Value);
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
