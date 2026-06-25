using AttendanceSystem.Application.Common;
using AttendanceSystem.Application.DTOs.Attendance;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Application.Interfaces.Repositories;
using AttendanceSystem.Application.Services;
using AttendanceSystem.Domain.Enums;
using MediatR;

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

    private const double UtcOffsetHours = 8; // Ulaanbaatar Time (UTC+8)

    public CheckOutCommandHandler(
        IAttendanceRepository attendanceRepository,
        IEmployeeRepository employeeRepository,
        IUnitOfWork unitOfWork,
        IGeofenceService geofenceService,
        AttendanceRulesService rulesService)
    {
        _attendanceRepository = attendanceRepository;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
        _geofenceService = geofenceService;
        _rulesService = rulesService;
    }

    /// <inheritdoc />
    public async Task<Result<AttendanceRecordDto>> Handle(CheckOutCommand request, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (employee?.WorkSchedule is null || employee.OfficeLocation is null)
            return Result<AttendanceRecordDto>.Failure("Employee not configured.", "CONFIG_MISSING");

        var todayLocal = DateOnly.FromDateTime(DateTime.Now.AddHours(UtcOffsetHours));
        var record = await _attendanceRepository.GetTodayRecordAsync(request.EmployeeId, todayLocal, cancellationToken);
        if (record is null)
            return Result<AttendanceRecordDto>.Failure("No check-in found for today.", "NO_CHECKIN");

        if (record.CheckOutTime.HasValue)
            return Result<AttendanceRecordDto>.Failure("Already checked out.", "ALREADY_CHECKED_OUT");

        if (request.Latitude is not null && request.Longitude is not null &&
            !_geofenceService.IsWithinRadius(
                request.Latitude.Value, request.Longitude.Value,
                employee.OfficeLocation.Latitude, employee.OfficeLocation.Longitude,
                employee.OfficeLocation.RadiusMeters))
            return Result<AttendanceRecordDto>.Failure("Та ажлын байрнаас хол байна", "OUT_OF_RANGE");

        var checkOutTime = DateTime.Now;
        var workDuration = checkOutTime - record.CheckInTime;
        var breakDuration = _rulesService.CalculateBreakDuration(workDuration, employee.WorkSchedule);
        var localDate = DateOnly.FromDateTime(record.CheckInTime.AddHours(UtcOffsetHours));
        var isWeekend = localDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var overtime = _rulesService.CalculateOvertimeHours(workDuration, breakDuration, employee.WorkSchedule, isWeekend, false);
        var shortHours = _rulesService.CalculateShortHours(workDuration, breakDuration, employee.WorkSchedule);
        var status = _rulesService.EvaluateCheckOut(record.CheckInTime, checkOutTime, employee.WorkSchedule, record.Status);

        Enum.TryParse<VerificationMethod>(request.VerificationMethod, true, out var method);
        record.CheckOut(checkOutTime, status, breakDuration, overtime, shortHours, method,
            request.Latitude, request.Longitude, null);

        _attendanceRepository.Update(record);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<AttendanceRecordDto>.Success(new AttendanceRecordDto(
            record.Id, record.EmployeeId, record.Date, record.CheckInTime, record.CheckOutTime,
            record.Status, record.OvertimeHours, record.LateMinutes, record.ShortHours, record.VerificationMethod,
            record.IsSuspicious, record.IsAutoGeo));
    }
}