using AttendanceSystem.Application.Configuration;
using AttendanceSystem.Application.Services;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace AttendanceSystem.UnitTests.Services;

/// <summary>
/// Unit tests for attendance rules engine.
/// </summary>
public class AttendanceRulesServiceTests
{
    private readonly AttendanceRulesService _sut;
    private readonly Mock<ApplicationDbContext> _dbContextMock;
    private readonly Mock<DbSet<Holiday>> _mockHolidayDbSet;

    public AttendanceRulesServiceTests()
    {
        var options = Options.Create(new AttendanceRulesOptions
        {
            DefaultGraceMinutes = 10,
            EarlyCheckinThresholdMinutes = 120,
            HalfDayLateThresholdMinutes = 180
        });

        // Setup mock DbContext and DbSet for Holiday
        _mockHolidayDbSet = new Mock<DbSet<Holiday>>();
        _dbContextMock = new Mock<ApplicationDbContext>();
        _dbContextMock.Setup(m => m.Holidays).Returns(_mockHolidayDbSet.Object);

        // Setup IsHolidayAsync to return false by default (not a holiday)
        _dbContextMock.Setup(m => m.Holidays.AnyAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Holiday, bool>>>(),
                It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(false);

        _sut = new AttendanceRulesService(options, _dbContextMock.Object);
    }

    [Fact]
    public async Task EvaluateCheckIn_OnTime_ReturnsPresent()
    {
        var schedule = WorkSchedule.CreateStandard();
        var date = new DateOnly(2024, 6, 3);
        var checkIn = date.ToDateTime(new TimeOnly(8, 5), DateTimeKind.Unspecified);

        var (status, late, _, _) = await _sut.EvaluateCheckIn(checkIn, schedule);

        status.Should().Be(AttendanceStatus.Present);
        late.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateCheckIn_AfterGrace_ReturnsLate()
    {
        var schedule = WorkSchedule.CreateStandard();
        var date = new DateOnly(2024, 6, 3);
        var checkIn = date.ToDateTime(new TimeOnly(9, 25), DateTimeKind.Unspecified);

        var (status, late, _, _) = await _sut.EvaluateCheckIn(checkIn, schedule);

        status.Should().Be(AttendanceStatus.Late);
        late.Should().BeGreaterThan(10);
    }

    [Fact]
    public async Task EvaluateCheckIn_MoreThanThreeHoursLate_ReturnsHalfDay()
    {
        var schedule = WorkSchedule.CreateStandard();
        var date = new DateOnly(2024, 6, 3);
        var checkIn = date.ToDateTime(new TimeOnly(13, 0), DateTimeKind.Unspecified);

        var (status, _, _, isHalfDay) = await _sut.EvaluateCheckIn(checkIn, schedule);

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
    public async Task CalculateOvertimeHours_ExceedsStandard_ReturnsPositive()
    {
        var schedule = WorkSchedule.CreateStandard();
        var work = TimeSpan.FromHours(10);
        var breakDuration = TimeSpan.FromHours(1);
        var overtime = _sut.CalculateOvertimeHours(work, breakDuration, schedule, false, false);
        overtime.Should().Be(1);
    }

    [Fact]
    public async Task EvaluateCheckIn_OnHoliday_ReturnsHolidayStatus()
    {
        // Arrange
        var schedule = WorkSchedule.CreateStandard();
        var date = new DateOnly(2024, 6, 3); // Monday
        var checkIn = date.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Unspecified);

        // Setup mock to return true for this date (holiday)
        _dbContextMock.Setup(m => m.Holidays.AnyAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Holiday, bool>>>(),
                It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var (status, late, _, _) = await _sut.EvaluateCheckIn(checkIn, schedule);

        // Assert
        status.Should().Be(AttendanceStatus.Holiday);
        late.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateCheckIn_OnWeekend_ReturnsWeekendWorkStatus()
    {
        // Arrange
        var schedule = WorkSchedule.CreateStandard();
        // Saturday
        var date = new DateOnly(2024, 6, 1);
        var checkIn = date.ToDateTime(new TimeOnly(9, 0), DateTimeKind.Unspecified);

        // Make sure holiday check returns false so weekend takes effect
        _dbContextMock.Setup(m => m.Holidays.AnyAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Holiday, bool>>>(),
                It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var (status, late, _, _) = await _sut.EvaluateCheckIn(checkIn, schedule);

        // Assert
        status.Should().Be(AttendanceStatus.WeekendWork);
        late.Should().Be(0);
    }

    [Fact]
    public async Task EvaluateCheckIn_OnNightShift_ReturnsNightShiftStatus()
    {
        // Arrange
        var schedule = WorkSchedule.CreateNightShift();
        var date = new DateOnly(2024, 6, 3); // Monday
        var checkIn = date.ToDateTime(new TimeOnly(22, 0), DateTimeKind.Unspecified); // 10 PM

        // Make sure holiday check returns false so night shift takes effect
        _dbContextMock.Setup(m => m.Holidays.AnyAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Holiday, bool>>>(),
                It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var (status, late, _, _) = await _sut.EvaluateCheckIn(checkIn, schedule);

        // Assert
        status.Should().Be(AttendanceStatus.NightShift);
        // Late minutes should still be calculated based on shift start time
        // Shift starts at 22:00, check-in at 22:00, so late should be 0
        late.Should().Be(0);
    }
}
}
