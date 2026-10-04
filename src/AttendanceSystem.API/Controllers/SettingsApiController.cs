using System.ComponentModel.DataAnnotations;
using AttendanceSystem.Domain;
using AttendanceSystem.Domain.Entities;
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

    [HttpPost("office-locations")]
    [Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.HrManager)]
    public async Task<ActionResult<OfficeLocationSettingsApiDto>> CreateOfficeLocation(
        [FromBody] OfficeLocationRequest request,
        CancellationToken cancellationToken)
    {
        var office = OfficeLocation.Create(
            request.Name.Trim(),
            request.Latitude,
            request.Longitude,
            request.RadiusMeters);
        if (!request.IsActive)
            office.Update(request.Name.Trim(), request.Latitude, request.Longitude, request.RadiusMeters, false);

        _db.OfficeLocations.Add(office);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new OfficeLocationSettingsApiDto(
            office.Id, office.Name, office.Latitude, office.Longitude, office.RadiusMeters, office.IsActive));
    }

    [HttpPut("office-locations/{id:guid}")]
    [Authorize(Roles = AppRoles.SuperAdmin + "," + AppRoles.HrManager)]
    public async Task<ActionResult<OfficeLocationSettingsApiDto>> UpdateOfficeLocation(
        Guid id,
        [FromBody] OfficeLocationRequest request,
        CancellationToken cancellationToken)
    {
        var office = await _db.OfficeLocations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (office is null)
            return NotFound();

        office.Update(request.Name.Trim(), request.Latitude, request.Longitude, request.RadiusMeters, request.IsActive);
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new OfficeLocationSettingsApiDto(
            office.Id, office.Name, office.Latitude, office.Longitude, office.RadiusMeters, office.IsActive));
    }
}

public sealed record CompanySettingsApiDto(string? CompanyName, string? TimeZone, string? DateFormat, string? TimeFormat, string? LogoUrl);
public sealed record AttendanceRulesApiDto(int GraceMinutes, bool RequireGpsForCheckIn, bool RequireGpsForCheckOut, bool AllowRemoteCheckIn, bool OvertimeEnabled);
public sealed record WorkScheduleSettingsApiDto(string? Name, TimeOnly? ShiftStart, TimeOnly? ShiftEnd, int BreakDurationMinutes, decimal StandardHoursPerDay);
public sealed record OfficeLocationSettingsApiDto(Guid Id, string Name, double Latitude, double Longitude, int RadiusMeters, bool IsActive);
public sealed record OfficeLocationRequest(
    [Required, StringLength(200), RegularExpression(@".*\S.*", ErrorMessage = "Office name is required.")]
    string Name,
    [Range(-90, 90)] double Latitude,
    [Range(-180, 180)] double Longitude,
    [Range(1, 50000)] int RadiusMeters,
    bool IsActive);
public sealed record GpsSettingsApiDto(bool Enabled, double OfficeLatitude, double OfficeLongitude, double AllowedRadiusMeters, bool BlockOutsideRadius);
