using AttendanceSystem.Application.Common;
using AttendanceSystem.Application.DTOs.Attendance;
using MediatR;

namespace AttendanceSystem.Application.Features.Attendance.Queries.GetTodayAttendance;

/// <summary>
/// Query for today's attendance record for an employee.
/// </summary>
public record GetTodayAttendanceQuery(Guid EmployeeId) : IRequest<Result<AttendanceRecordDto?>>;
