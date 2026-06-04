using AttendanceSystem.Application.Common;
using AttendanceSystem.Application.Configuration;
using AttendanceSystem.Application.DTOs.Attendance;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Application.Interfaces.Repositories;
using AttendanceSystem.Application.Services;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Options;

namespace AttendanceSystem.Application.Features.Attendance.Commands.CheckIn;

/// <summary>
/// Handles employee check-in with GPS validation and rules engine.
/// </summary>
public class CheckInCommandHandler : IRequestHandler<CheckInCommand, Result<AttendanceRecordDto>>
{
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGeofenceService _geofenceService;
    private readonly AttendanceRulesService _rulesService;
    private readonly AttendanceRulesOptions _options;

    public CheckInCommandHandler(
        IAttendanceRepository attendanceRepository,
        IEmployeeRepository employeeRepository,
        IUnitOfWork unitOfWork,
        IGeofenceService geofenceService,
        AttendanceRulesService rulesService,
        IOptions<AttendanceRulesOptions> options)
    {
        _attendanceRepository = attendanceRepository;
        _employeeRepository = employeeRepository;
        _unitOfWork = unitOfWork;
        _geofenceService = geofenceService;
        _rulesService = rulesService;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<Result<AttendanceRecordDto>> Handle(CheckInCommand request, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (employee is null || !employee.IsActive)
            return Result<AttendanceRecordDto>.Failure("Employee not found.", "EMPLOYEE_NOT_FOUND");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var existing = await _attendanceRepository.GetTodayRecordAsync(request.EmployeeId, today, cancellationToken);
        if (existing is not null)
            return Result<AttendanceRecordDto>.Failure("Already checked in today.", "ALREADY_CHECKED_IN");

        var schedule = employee.WorkSchedule;
        var office = employee.OfficeLocation;
        if (schedule is null || office is null)
            return Result<AttendanceRecordDto>.Failure("Employee schedule or office not configured.", "CONFIG_MISSING");

        var checkInTime = DateTime.UtcNow;
        var status = AttendanceStatus.Present;
        decimal lateMinutes = 0;
        var isSuspicious = false;

        if (!request.LocationPermissionGranted)
        {
            status = AttendanceStatus.PendingManualReview;
        }
        else if (request.Latitude is null || request.Longitude is null)
        {
            return Result<AttendanceRecordDto>.Failure("GPS coordinates required.", "GPS_REQUIRED");
        }
        else if (!_geofenceService.IsWithinRadius(
                     request.Latitude.Value, request.Longitude.Value,
                     office.Latitude, office.Longitude, office.RadiusMeters))
        {
            var distance = _geofenceService.CalculateDistanceMeters(
                request.Latitude.Value, request.Longitude.Value, office.Latitude, office.Longitude);
            if (distance > _options.SuspiciousDistanceMeters)
                isSuspicious = true;
            return Result<AttendanceRecordDto>.Failure("Та ажлын байрнаас хол байна", "OUT_OF_RANGE");
        }
        else
        {
            var evaluation = _rulesService.EvaluateCheckIn(checkInTime, schedule, today);
            status = evaluation.Status;
            lateMinutes = evaluation.LateMinutes;
            if (evaluation.IsVeryEarly)
                isSuspicious = true;
        }

        if (!Enum.TryParse<VerificationMethod>(request.VerificationMethod, true, out var method))
            method = VerificationMethod.Gps;

        var record = AttendanceRecord.CreateCheckIn(
            request.EmployeeId,
            checkInTime,
            status,
            lateMinutes,
            method,
            isManual: false,
            isSuspicious,
            isAutoGeo: false,
            request.Latitude,
            request.Longitude,
            photoUrl: null,
            notes: null);

        await _attendanceRepository.AddAsync(record, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<AttendanceRecordDto>.Success(MapToDto(record));
    }

    private static AttendanceRecordDto MapToDto(AttendanceRecord r) => new(
        r.Id, r.EmployeeId, r.Date, r.CheckInTime, r.CheckOutTime,
        r.Status, r.OvertimeHours, r.LateMinutes, r.VerificationMethod, r.IsSuspicious, r.IsAutoGeo);
}
