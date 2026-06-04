using AttendanceSystem.Application.Common;
using AttendanceSystem.Application.DTOs.Attendance;
using MediatR;

namespace AttendanceSystem.Application.Features.Attendance.Queries.GetAttendanceStatistics;

/// <summary>
/// Query for attendance statistics for the current month.
/// </summary>
public record GetAttendanceStatisticsQuery(Guid EmployeeId) : IRequest<Result<AttendanceStatisticsDto>>;
