using AttendanceSystem.Application.Common;
using AttendanceSystem.Application.DTOs.Attendance;
using AttendanceSystem.Application.Interfaces.Repositories;
using MediatR;

namespace AttendanceSystem.Application.Features.Attendance.Queries.GetTodayAttendance;

/// <summary>
/// Returns today's attendance for the employee.
/// </summary>
public class GetTodayAttendanceQueryHandler : IRequestHandler<GetTodayAttendanceQuery, Result<AttendanceRecordDto?>>
{
    private readonly IAttendanceRepository _repository;

    public GetTodayAttendanceQueryHandler(IAttendanceRepository repository) => _repository = repository;

    /// <inheritdoc />
    public async Task<Result<AttendanceRecordDto?>> Handle(GetTodayAttendanceQuery request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var record = await _repository.GetTodayRecordAsync(request.EmployeeId, today, cancellationToken);
        if (record is null)
            return Result<AttendanceRecordDto?>.Success(null);

        return Result<AttendanceRecordDto?>.Success(new AttendanceRecordDto(
            record.Id, record.EmployeeId, record.Date, record.CheckInTime, record.CheckOutTime,
            record.Status, record.OvertimeHours, record.LateMinutes, record.VerificationMethod,
            record.IsSuspicious, record.IsAutoGeo));
    }
}
