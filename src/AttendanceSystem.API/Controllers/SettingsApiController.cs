using AttendanceSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.API.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public sealed class SettingsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public SettingsApiController(ApplicationDbContext db) => _db = db;

    [HttpGet("company")]
    public ActionResult<CompanySettingsApiDto> Company()
        => Ok(new CompanySettingsApiDto(null, null, null, null, null));

    [HttpGet("attendance-rules")]
    public async Task<ActionResult<AttendanceRulesApiDto>> AttendanceRules(CancellationToken cancellationToken)
    {
        var schedule = await _db.WorkSchedules.AsNoTracking().OrderBy(w => w.Name).FirstOrDefaultAsync(cancellationToken);
        return Ok(new AttendanceRulesApiDto(schedule?.GraceMinutes ?? 0, true, true, false, true));
    }

    [HttpGet("work-schedule")]
    public async Task<ActionResult<WorkScheduleSettingsApiDto?>> WorkSchedule(CancellationToken cancellationToken)
    {
        var schedule = await _db.WorkSchedules.AsNoTracking().OrderBy(w => w.Name).FirstOrDefaultAsync(cancellationToken);
        if (schedule is null)
            return Ok(null);

        return Ok(new WorkScheduleSettingsApiDto(
            schedule.Name,
            schedule.ShiftStart,
            schedule.ShiftEnd,
            schedule.BreakDurationMinutes,
            schedule.StandardHoursPerDay));
    }

    [HttpGet("gps")]
    public async Task<ActionResult<GpsSettingsApiDto>> Gps(CancellationToken cancellationToken)
    {
        var office = await _db.OfficeLocations.AsNoTracking().Where(o => o.IsActive).OrderBy(o => o.Name).FirstOrDefaultAsync(cancellationToken);
        return Ok(new GpsSettingsApiDto(
            office is not null,
            office?.Latitude ?? 0,
            office?.Longitude ?? 0,
            office?.RadiusMeters ?? 0,
            office is not null));
    }

    [HttpGet("office-locations")]
    public async Task<ActionResult<IReadOnlyList<OfficeLocationSettingsApiDto>>> OfficeLocations(CancellationToken cancellationToken)
    {
        var offices = await _db.OfficeLocations
            .AsNoTracking()
            .OrderBy(o => o.Name)
            .Select(o => new OfficeLocationSettingsApiDto(o.Id, o.Name, o.Latitude, o.Longitude, o.RadiusMeters, o.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(offices);
    }
}

public sealed record CompanySettingsApiDto(string? CompanyName, string? TimeZone, string? DateFormat, string? TimeFormat, string? LogoUrl);
public sealed record AttendanceRulesApiDto(int GraceMinutes, bool RequireGpsForCheckIn, bool RequireGpsForCheckOut, bool AllowRemoteCheckIn, bool OvertimeEnabled);
public sealed record WorkScheduleSettingsApiDto(string? Name, TimeOnly? ShiftStart, TimeOnly? ShiftEnd, int BreakDurationMinutes, decimal StandardHoursPerDay);
public sealed record OfficeLocationSettingsApiDto(Guid Id, string Name, double Latitude, double Longitude, int RadiusMeters, bool IsActive);
public sealed record GpsSettingsApiDto(bool Enabled, double OfficeLatitude, double OfficeLongitude, double AllowedRadiusMeters, bool BlockOutsideRadius);
