using AttendanceSystem.Application.Common;
using AttendanceSystem.Application.DTOs.Attendance;
using MediatR;

namespace AttendanceSystem.Application.Features.Attendance.Commands.CheckIn;

/// <summary>
/// Command to record employee check-in.
/// </summary>
public record CheckInCommand(
    Guid EmployeeId,
    double? Latitude,
    double? Longitude,
    bool LocationPermissionGranted,
    string? PhotoBase64,
    string VerificationMethod) : IRequest<Result<AttendanceRecordDto>>;
