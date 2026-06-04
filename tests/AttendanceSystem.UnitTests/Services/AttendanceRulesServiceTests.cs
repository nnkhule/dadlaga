using AttendanceSystem.Application.Configuration;
using AttendanceSystem.Application.Services;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace AttendanceSystem.UnitTests.Services;

/// <summary>
/// Unit tests for attendance rules engine.
/// </summary>
public class AttendanceRulesServiceTests
{
    private readonly AttendanceRulesService _sut;

    public AttendanceRulesServiceTests()
    {
        var options = Options.Create(new AttendanceRulesOptions
        {
            DefaultGraceMinutes = 10,
            EarlyCheckinThresholdMinutes = 120,
            HalfDayLateThresholdMinutes = 180
        });
        _sut = new AttendanceRulesService(options);
    }

    [Fact]
    public void EvaluateCheckIn_OnTime_ReturnsPresent()
    {
        var schedule = WorkSchedule.CreateStandard();
        var date = new DateOnly(2024, 6, 3);
        var checkIn = date.ToDateTime(new TimeOnly(9, 5), DateTimeKind.Unspecified);

        var (status, late, _, _) = _sut.EvaluateCheckIn(checkIn, schedule, date);

        status.Should().Be(AttendanceStatus.Present);
        late.Should().Be(0);
    }

    [Fact]
    public void EvaluateCheckIn_AfterGrace_ReturnsLate()
    {
        var schedule = WorkSchedule.CreateStandard();
        var date = new DateOnly(2024, 6, 3);
        var checkIn = date.ToDateTime(new TimeOnly(9, 25), DateTimeKind.Unspecified);

        var (status, late, _, _) = _sut.EvaluateCheckIn(checkIn, schedule, date);

        status.Should().Be(AttendanceStatus.Late);
        late.Should().BeGreaterThan(10);
    }

    [Fact]
    public void EvaluateCheckIn_MoreThanThreeHoursLate_ReturnsHalfDay()
    {
        var schedule = WorkSchedule.CreateStandard();
        var date = new DateOnly(2024, 6, 3);
        var checkIn = date.ToDateTime(new TimeOnly(13, 0), DateTimeKind.Unspecified);

        var (status, _, _, isHalfDay) = _sut.EvaluateCheckIn(checkIn, schedule, date);

        status.Should().Be(AttendanceStatus.HalfDay);
        isHalfDay.Should().BeTrue();
    }

    [Fact]
    public void CalculateBreakDuration_UnderFourHours_NoBreak()
    {
        var schedule = WorkSchedule.CreateStandard();
        var duration = TimeSpan.FromHours(3.5);
        _sut.CalculateBreakDuration(duration, schedule).Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void CalculateBreakDuration_OverSixHours_SixtyMinuteBreak()
    {
        var schedule = WorkSchedule.CreateStandard();
        var duration = TimeSpan.FromHours(8);
        _sut.CalculateBreakDuration(duration, schedule).TotalMinutes.Should().Be(60);
    }

    [Fact]
    public void CalculateOvertimeHours_ExceedsStandard_ReturnsPositive()
    {
        var schedule = WorkSchedule.CreateStandard();
        var work = TimeSpan.FromHours(10);
        var breakDuration = TimeSpan.FromHours(1);
        var overtime = _sut.CalculateOvertimeHours(work, breakDuration, schedule, false, false);
        overtime.Should().Be(1);
    }
}
