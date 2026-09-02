using AttendanceSystem.Application.Common;
using AttendanceSystem.Application.Configuration;
using AttendanceSystem.Application.DTOs.Attendance;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Application.Interfaces.Repositories;
using AttendanceSystem.Application.Services;
using AttendanceSystem.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace AttendanceSystem.Application.Features.Attendance.Commands.CheckOut;

/// <summary>
/// Handles employee check-out with overtime and break calculation.
/// </summary>
public class CheckOutCommandHandler : IRequestHandler<CheckOutCommand, Result<AttendanceRecordDto>>
{
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGeofenceService _geofenceService;
    private readonly AttendanceRulesService _rulesService;
    private readonly IOptions<AttendanceRulesOptions> _options;
    private readonly IClock _clock;
    private readonly IHolidayRepository _holidayRepository;

    public CheckOutCommandHandler(
        IAttendanceRepository attendanceRepository,
        IEmployeeRepository employeeRepository,
        IUnitOfWork unitOfWork,
        IGeofenceService geofenceService,
        AttendanceRulesService rulesService,
        IOptions<AttendanceRulesOptions> options,
        IClock clock,
        IHolidayRepository holidayRepository)
    {
        _attendanceRepository = attendanceRepository;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
        _geofenceService = geofenceService;
        _rulesService = rulesService;
        _options = options;
        _clock = clock;
        _holidayRepository = holidayRepository;
    }

    /// <inheritdoc />
    public async Task<Result<AttendanceRecordDto>> Handle(CheckOutCommand request, CancellationToken ct)
    {
        var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId, ct);
        if (employee?.WorkSchedule is null || employee.OfficeLocation is null)
            return Result<AttendanceRecordDto>.Failure("Employee not configured.", "CONFIG_MISSING");

        var todayLocal = _clock.TodayLocal;
        var record = await _attendanceRepository.GetTodayRecordAsync(request.EmployeeId, todayLocal, ct);
        if (record is null)
            return Result<AttendanceRecordDto>.Failure("No check-in found for today.", "NO_CHECKIN");

        if (record.CheckOutTime.HasValue)
            return Result<AttendanceRecordDto>.Failure("Already checked out.", "ALREADY_CHECKED_OUT");

        // Check GPS requirements based on configuration
        bool requireGpsForCheckOut = _options.Value.RequireGpsForCheckOut;
        if (requireGpsForCheckOut)
        {
            // GPS is required for check-out - follow same pattern as check-in
            if (request.Latitude is null || request.Longitude is null)
                return Result<AttendanceRecordDto>.Failure("GPS coordinates required.", "GPS_REQUIRED");

            if (!_geofenceService.IsWithinRadius(
                    request.Latitude.Value, request.Longitude.Value,
                    employee.OfficeLocation.Latitude, employee.OfficeLocation.Longitude,
                    employee.OfficeLocation.RadiusMeters))
            {
                var distance = _geofenceService.CalculateDistanceMeters(
                    request.Latitude.Value, request.Longitude.Value,
                    employee.OfficeLocation.Latitude, employee.OfficeLocation.Longitude);
                // Note: We don't currently have an IsSuspicious flag for check-out like check-in does,
                // but if needed, we could add it to the CheckOut method or track it elsewhere
                return Result<AttendanceRecordDto>.Failure("Та ажлын байрнаас хол байна", "OUT_OF_RANGE");
            }
        }
        else
        {
            // GPS is not required for check-out - but validate if provided
            if (request.Latitude is not null && request.Longitude is not null &&
                !_geofenceService.IsWithinRadius(
                    request.Latitude.Value, request.Longitude.Value,
                    employee.OfficeLocation.Latitude, employee.OfficeLocation.Longitude,
                    employee.OfficeLocation.RadiusMeters))
            {
                // Coordinates provided but outside geofence - log or handle as needed
                // For now, we allow check-out to proceed but could flag for review
            }
        }

        var checkOutTime = _clock.LocalNow;
        var workDuration = checkOutTime - record.CheckInTime;
        var breakDuration = _rulesService.CalculateBreakDuration(workDuration, employee.WorkSchedule);
        var isWeekend = record.Date.DayOfWeek == DayOfWeek.Saturday ||
                       record.Date.DayOfWeek == DayOfWeek.Sunday;
        var isHoliday = await _holidayRepository.IsHolidayAsync(todayLocal, ct);
        var overtime = _rulesService.CalculateOvertimeHours(
            workDuration, breakDuration, employee.WorkSchedule, isWeekend, isHoliday);
        var shortHours = _rulesService.CalculateShortHours(workDuration, breakDuration, employee.WorkSchedule);
        var status = _rulesService.EvaluateCheckOut(
            record.CheckInTime, checkOutTime, employee.WorkSchedule, record.Status);

        Enum.TryParse<VerificationMethod>(request.VerificationMethod, true, out var method);
        record.CheckOut(checkOutTime, status, breakDuration, overtime, shortHours, method,
            request.Latitude, request.Longitude, null);

        _attendanceRepository.Update(record);
        await _unitOfWork.SaveChangesAsync(ct);

        return Result<AttendanceRecordDto>.Success(new AttendanceRecordDto(
            record.Id, record.EmployeeId, record.Date, record.CheckInTime, record.CheckOutTime,
            record.Status, record.OvertimeHours, record.LateMinutes, record.ShortHours,
            record.VerificationMethod, record.IsSuspicious, record.IsAutoGeo));
    }
}