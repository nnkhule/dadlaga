using AttendanceSystem.Application.Configuration;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using Microsoft.Extensions.Options;

namespace AttendanceSystem.Application.Services;

/// <summary>
/// Core attendance rules engine for check-in, check-out, breaks, and overtime.
/// </summary>
public class AttendanceRulesService
{
    private readonly AttendanceRulesOptions _options;

    public AttendanceRulesService(IOptions<AttendanceRulesOptions> options)
        => _options = options.Value;

    /// <summary>
    /// Evaluates check-in time against schedule and returns status and late minutes.
    /// </summary>
    public (AttendanceStatus Status, decimal LateMinutes, bool IsVeryEarly, bool IsHalfDay) EvaluateCheckIn(
        DateTime checkInUtc, WorkSchedule schedule, DateOnly localDate)
    {
        var shiftStart = localDate.ToDateTime(schedule.ShiftStart, DateTimeKind.Unspecified);
        var graceEnd = shiftStart.AddMinutes(schedule.GraceMinutes);
        var halfDayThreshold = shiftStart.AddMinutes(_options.HalfDayLateThresholdMinutes);
        var earlyThreshold = shiftStart.AddMinutes(-_options.EarlyCheckinThresholdMinutes);

        var isVeryEarly = checkInUtc < earlyThreshold;
        if (checkInUtc <= graceEnd)
            return (AttendanceStatus.Present, 0, isVeryEarly, false);

        var lateMinutes = (decimal)(checkInUtc - shiftStart).TotalMinutes;
        if (checkInUtc >= halfDayThreshold)
            return (AttendanceStatus.HalfDay, lateMinutes, isVeryEarly, true);

        return (AttendanceStatus.Late, lateMinutes, isVeryEarly, false);
    }

    /// <summary>
    /// Evaluates check-out against scheduled end time.
    /// </summary>
    public AttendanceStatus EvaluateCheckOut(DateTime checkOutUtc, WorkSchedule schedule, DateOnly localDate,
        AttendanceStatus currentStatus)
    {
        var shiftEnd = localDate.ToDateTime(schedule.ShiftEnd, DateTimeKind.Unspecified);
        if (checkOutUtc < shiftEnd && currentStatus is AttendanceStatus.Present or AttendanceStatus.Late)
            return AttendanceStatus.EarlyLeave;
        return currentStatus == AttendanceStatus.Late ? AttendanceStatus.Late : AttendanceStatus.Present;
    }

    /// <summary>
    /// Calculates break duration per company policy based on work hours.
    /// </summary>
    public TimeSpan CalculateBreakDuration(TimeSpan workDuration, WorkSchedule schedule)
    {
        var hours = workDuration.TotalHours;
        if (hours < _options.ShortShiftNoBreakHours)
            return TimeSpan.Zero;
        if (hours <= 6)
            return TimeSpan.FromMinutes(_options.MediumShiftBreakMinutes);
        return TimeSpan.FromMinutes(schedule.BreakDurationMinutes > 0
            ? schedule.BreakDurationMinutes
            : _options.LongShiftBreakMinutes);
    }

    /// <summary>
    /// Calculates overtime hours with optional multipliers.
    /// </summary>
    public decimal CalculateOvertimeHours(
        TimeSpan workDuration,
        TimeSpan breakDuration,
        WorkSchedule schedule,
        bool isWeekend,
        bool isHoliday)
    {
        var actualHours = (decimal)(workDuration - breakDuration).TotalHours;
        var standard = schedule.StandardHoursPerDay;
        var overtime = Math.Max(0, actualHours - standard);

        if (isHoliday) return overtime * 2.0m;
        if (isWeekend) return overtime * schedule.WeekendMultiplier;
        if (schedule.IsNightShift) return overtime * schedule.NightShiftMultiplier;
        return overtime;
    }
}
