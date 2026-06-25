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
    private const double UtcOffsetHours = 8; // Ulaanbaatar Time (UTC+8)

    public AttendanceRulesService(IOptions<AttendanceRulesOptions> options)
        => _options = options.Value;

    /// <summary>
    /// Evaluates check-in time against schedule and returns status and late minutes.
    /// </summary>
    public (AttendanceStatus Status, decimal LateMinutes, bool IsVeryEarly, bool IsHalfDay) EvaluateCheckIn(
        DateTime checkInTime, WorkSchedule schedule)
    {
        // Treat input as local time
        var checkInLocal = checkInTime;
        var localDate = DateOnly.FromDateTime(checkInLocal);

        // For night shifts that cross midnight, adjust the date if check-in is before shift start
        DateTime shiftStart;
        if (schedule.IsNightShift && checkInLocal.TimeOfDay < schedule.ShiftStart.ToTimeSpan())
        {
            // Shift started yesterday
            var shiftStartDate = localDate.AddDays(-1);
            shiftStart = shiftStartDate.ToDateTime(schedule.ShiftStart, DateTimeKind.Unspecified);
        }
        else
        {
            shiftStart = localDate.ToDateTime(schedule.ShiftStart, DateTimeKind.Unspecified);
        }

        var graceEnd = shiftStart.AddMinutes(schedule.GraceMinutes);
        var halfDayThreshold = shiftStart.AddMinutes(_options.HalfDayLateThresholdMinutes);
        var earlyThreshold = shiftStart.AddMinutes(-_options.EarlyCheckinThresholdMinutes);

        var isVeryEarly = checkInLocal < earlyThreshold;
        if (checkInLocal <= graceEnd)
        {
            return (AttendanceStatus.Present, 0, isVeryEarly, false);
        }

        var lateMinutes = (decimal)(checkInLocal - shiftStart).TotalMinutes;
        if (checkInLocal >= halfDayThreshold)
        {
            return (AttendanceStatus.HalfDay, lateMinutes, isVeryEarly, true);
        }

        return (AttendanceStatus.Late, lateMinutes, isVeryEarly, false);
    }

    /// <summary>
    /// Evaluates check-out against scheduled end time.
    /// </summary>
    /// <remarks>
    /// Previous (buggy) implementation flagged EarlyLeave for ANY checkout before ShiftEnd,
    /// with no grace period and no regard for how many hours were actually worked. That meant
    /// an employee who checked in early and worked a full standard day, but happened to check
    /// out a minute before the nominal shift-end clock time, was incorrectly marked EarlyLeave.
    ///
    /// Correct rule: EarlyLeave only applies when checkout is before (ShiftEnd - grace period)
    /// AND the employee did not complete a standard work day (worked hours less than StandardHoursPerDay).
    /// An employee who completed a full standard day is never EarlyLeave, regardless of the
    /// clock time they checked out at.
    /// </remarks>
    public AttendanceStatus EvaluateCheckOut(DateTime checkInTime, DateTime checkOutTime, WorkSchedule schedule,
        AttendanceStatus currentStatus, int graceMinutes = 15)
    {
        // Statuses that are already final/non-attendance-derived must not be overwritten.
        if (currentStatus is AttendanceStatus.OnLeave or AttendanceStatus.Holiday
            or AttendanceStatus.PendingManualReview)
            return currentStatus;

        // Input times are already local time
        var checkInLocal = checkInTime;
        var checkOutLocal = checkOutTime;
        var localDate = DateOnly.FromDateTime(checkInLocal); // assume same day

        // For night shifts that cross midnight, adjust the date if check-in is before shift start
        DateOnly shiftStartDate = localDate;
        if (schedule.IsNightShift && checkInLocal.TimeOfDay < schedule.ShiftStart.ToTimeSpan())
        {
            // Shift started yesterday
            shiftStartDate = localDate.AddDays(-1);
        }

        bool isOvernightShift = schedule.ShiftEnd.ToTimeSpan() < schedule.ShiftStart.ToTimeSpan();
        DateTime shiftStart = shiftStartDate.ToDateTime(schedule.ShiftStart, DateTimeKind.Unspecified);
        DateTime shiftEnd;
        if (isOvernightShift)
        {
            shiftEnd = shiftStartDate.AddDays(1).ToDateTime(schedule.ShiftEnd, DateTimeKind.Unspecified);
        }
        else
        {
            shiftEnd = shiftStartDate.ToDateTime(schedule.ShiftEnd, DateTimeKind.Unspecified);
        }

        var graceEnd = shiftEnd.AddMinutes(-Math.Abs(graceMinutes));

        var workedHours = (decimal)(checkOutTime - checkInTime).TotalHours;
        var completedStandardDay = workedHours >= schedule.StandardHoursPerDay;

        var leftBeforeGrace = checkOutLocal < graceEnd;

        if (leftBeforeGrace && !completedStandardDay &&
            currentStatus is AttendanceStatus.Present or AttendanceStatus.Late or AttendanceStatus.HalfDay
                or AttendanceStatus.NightShift or AttendanceStatus.WeekendWork)
        {
            return AttendanceStatus.EarlyLeave;
        }

        // Preserve whatever check-in already determined (Late, HalfDay, NightShift,
        // WeekendWork, etc.) instead of collapsing everything to Present/Late.
        return currentStatus;
    }

    public decimal CalculateShortHours(
        TimeSpan workDuration,
        TimeSpan breakDuration,
        WorkSchedule schedule)
    {
        var actualHours =
            (decimal)(workDuration - breakDuration).TotalHours;

        return Math.Max(
            0,
            schedule.StandardHoursPerDay - actualHours);
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