using AttendanceSystem.Application.Common;
using AttendanceSystem.Application.DTOs.Attendance;
using MediatR;

namespace AttendanceSystem.Application.Features.Attendance.Commands.CheckOut;

/// <summary>
/// Command to record employee check-out.
/// </summary>
public record CheckOutCommand(
    Guid EmployeeId,
    double? Latitude,
    double? Longitude,
    string VerificationMethod) : IRequest<Result<AttendanceRecordDto>>;
