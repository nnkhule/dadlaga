using System.Linq;
using AttendanceSystem.Application.Common;
using AttendanceSystem.Application.DTOs.Attendance;
using AttendanceSystem.Application.Interfaces.Repositories;
using AttendanceSystem.Domain.Enums;
using MediatR;

namespace AttendanceSystem.Application.Features.Attendance.Queries.GetAttendanceStatistics;

/// <summary>
/// Handles monthly attendance statistics queries.
/// </summary>
public class GetAttendanceStatisticsQueryHandler : IRequestHandler<GetAttendanceStatisticsQuery, Result<AttendanceStatisticsDto>>
{
    private readonly IAttendanceRepository _attendanceRepository;
    private readonly IEmployeeRepository _employeeRepository;
    private readonly IClock _clock;

    public GetAttendanceStatisticsQueryHandler(
        IAttendanceRepository attendanceRepository,
        IEmployeeRepository employeeRepository,
        IClock clock)
    {
        _attendanceRepository = attendanceRepository;
        _employeeRepository = employeeRepository;
        _clock = clock;
    }

    public async Task<Result<AttendanceStatisticsDto>> Handle(GetAttendanceStatisticsQuery request, CancellationToken cancellationToken)
    {
        var employee = await _employeeRepository.GetByIdAsync(request.EmployeeId, cancellationToken);
        if (employee is null)
            return Result<AttendanceStatisticsDto>.Failure("Employee not found.", "EMPLOYEE_NOT_FOUND");

        if (employee.WorkSchedule is null)
            return Result<AttendanceStatisticsDto>.Failure("Employee work schedule not configured.", "SCHEDULE_MISSING");

        var today = _clock.TodayLocal;
        var startOfMonth = new DateOnly(today.Year, today.Month, 1);
        var records = await _attendanceRepository.GetByEmployeeAsync(request.EmployeeId, startOfMonth, today, cancellationToken);

        // compute recent records (last 5 values within the queried range)
        var recent = records
            .OrderByDescending(r => r.Date)
            .Take(5)
            .Select(r => new AttendanceHistoryItemDto(r.Date, r.CheckInTime, r.CheckOutTime))
            .ToList();

        var workingDays = CountWorkingDays(employee.WorkSchedule, startOfMonth, today);
        // Single source of truth: classify off AttendanceRecord.Status via
        // AttendanceStatusClassifier. Previously presentDays counted every record
        // (including Absent ones) and lateCount used LateMinutes>0 instead of Status.
        var presentDays = records.Count(x => AttendanceStatusClassifier.IsPresentBucket(x.Status));
        var lateCount = records.Count(x => AttendanceStatusClassifier.IsLate(x.Status));
        var overtimeHours = records.Sum(x => x.OvertimeHours);

        var monthLabel = today.ToString("MMMM yyyy");

        return Result<AttendanceStatisticsDto>.Success(new AttendanceStatisticsDto(
            presentDays,
            workingDays,
            lateCount,
            overtimeHours,
            monthLabel,
            recent));
    }

    private static int CountWorkingDays(Domain.Entities.WorkSchedule workSchedule, DateOnly from, DateOnly to)
    {
        var days = 0;
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if (workSchedule.IsWorkDay(day.DayOfWeek))
                days++;
        }

        return days;
    }
}