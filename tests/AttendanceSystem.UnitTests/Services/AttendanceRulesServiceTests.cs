using AttendanceSystem.Application.Common;
using AttendanceSystem.Application.Configuration;
using AttendanceSystem.Application.Features.Attendance.Commands.CheckIn;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Application.Interfaces.Repositories;
using AttendanceSystem.Application.Services;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace AttendanceSystem.UnitTests.Services;

/// <summary>
/// Unit tests for attendance rules engine.
/// </summary>
public class AttendanceRulesServiceTests
{
    private readonly AttendanceRulesService _sut;
    private readonly Mock<IHolidayRepository> _holidayRepositoryMock;

    public AttendanceRulesServiceTests()
    {
        var options = Options.Create(new AttendanceRulesOptions
        {
            DefaultGraceMinutes = 10,
            EarlyCheckinThresholdMinutes = 120,
            HalfDayLateThresholdMinutes = 180
        });

        _holidayRepositoryMock = new Mock<IHolidayRepository>();
        _holidayRepositoryMock.Setup(m => m.IsHolidayAsync(It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _sut = new AttendanceRulesService(options, _holidayRepositoryMock.Object);
    }

    [Fact]
    public async Task CheckInCommandHandler_AllowsCheckInOutsideOfficeRadius()
    {
        var attendanceRepository = new Mock<IAttendanceRepository>();
        attendanceRepository.Setup(r => r.GetTodayRecordAsync(It.IsAny<Guid>(), It.IsAny<DateOnly>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AttendanceRecord?)null);

        var employeeRepository = new Mock<IEmployeeRepository>();
        var workSchedule = WorkSchedule.CreateStandard();
        var office = OfficeLocation.Create("Head Office", 47.912216, 106.931346, 5000);
        var employee = Employee.Create(
            "EMP-100",
            "Test Employee",
            "test@attendance.local",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2024, 1, 1),
            ContractType.FullTime);

        typeof(Employee).GetProperty(nameof(Employee.WorkSchedule))!.SetValue(employee, workSchedule);
        typeof(Employee).GetProperty(nameof(Employee.OfficeLocation))!.SetValue(employee, office);

        employeeRepository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var geofenceService = new Mock<IGeofenceService>();
        geofenceService.Setup(s => s.CalculateDistanceMeters(47.9, 107.0, office.Latitude, office.Longitude))
            .Returns(6000d);

        var options = Options.Create(new AttendanceRulesOptions
        {
            DefaultGraceMinutes = 10,
            EarlyCheckinThresholdMinutes = 120,
            HalfDayLateThresholdMinutes = 180,
            SuspiciousDistanceMeters = 3000
        });

        var clock = new Mock<IClock>();
        var now = new DateTime(2024, 6, 3, 8, 0, 0, DateTimeKind.Unspecified);
        clock.Setup(c => c.TodayLocal).Returns(DateOnly.FromDateTime(now));
        clock.Setup(c => c.LocalNow).Returns(now);

        var handler = new CheckInCommandHandler(
            attendanceRepository.Object,
            employeeRepository.Object,
            unitOfWork.Object,
            geofenceService.Object,
            new AttendanceRulesService(options, new Mock<IHolidayRepository>().Object),
            options,
            clock.Object);

        var result = await handler.Handle(
            new CheckInCommand(employee.Id, 47.9, 107.0, true, null, "Gps"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Status.Should().Be(AttendanceStatus.Present);
    }

    [Fact]
    public async Task EvaluateCheckIn_OnTime_ReturnsPresent()
    {
        var schedule = WorkSchedule.CreateStandard();
        var date = new DateOnly(2024, 6, 3);
        var checkIn = date.ToDateTime(new TimeOnly(8, 0), DateTimeKind.Unspecified);

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
        _holidayRepositoryMock.Setup(m => m.IsHolidayAsync(date, It.IsAny<CancellationToken>()))
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
        _holidayRepositoryMock.Setup(m => m.IsHolidayAsync(date, It.IsAny<CancellationToken>()))
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
        _holidayRepositoryMock.Setup(m => m.IsHolidayAsync(date, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var (status, late, _, _) = await _sut.EvaluateCheckIn(checkIn, schedule);

        // Assert
        status.Should().Be(AttendanceStatus.NightShift);
        // Late minutes should still be calculated based on shift start time
        // Shift starts at 22:00, check-in at 22:00, so late should be 0
        late.Should().Be(0);
    }

    [Fact]
    public void CalculateOvertimeHours_WhenDisabled_ReturnsZero()
    {
        var options = Options.Create(new AttendanceRulesOptions { OvertimeEnabled = false });
        var service = new AttendanceRulesService(options, _holidayRepositoryMock.Object);
        var schedule = WorkSchedule.CreateStandard();

        var overtime = service.CalculateOvertimeHours(
            TimeSpan.FromHours(10),
            TimeSpan.FromHours(1),
            schedule,
            isWeekend: false,
            isHoliday: false);

        overtime.Should().Be(0);
    }

    [Fact]
    public void EvaluateCheckOut_UsesScheduleGraceAndDeductsBreakBeforeEarlyLeaveCheck()
    {
        var schedule = WorkSchedule.CreateStandard();
        var checkIn = new DateTime(2024, 6, 3, 9, 40, 0);
        var checkOut = new DateTime(2024, 6, 3, 16, 44, 0);

        var status = _sut.EvaluateCheckOut(checkIn, checkOut, schedule, AttendanceStatus.Present);

        status.Should().Be(AttendanceStatus.EarlyLeave);
    }

    [Fact]
    public void CreateStandard_UsesRequestedEightToFiveShiftWithoutGrace()
    {
        var schedule = WorkSchedule.CreateStandard();

        schedule.ShiftStart.Should().Be(new TimeOnly(8, 0));
        schedule.ShiftEnd.Should().Be(new TimeOnly(17, 0));
        schedule.GraceMinutes.Should().Be(0);
        schedule.BreakDurationMinutes.Should().Be(60);
        schedule.StandardHoursPerDay.Should().Be(8);
    }

    [Fact]
    public async Task EvaluateCheckIn_OneMinuteAfterShiftStart_IsLate()
    {
        var schedule = WorkSchedule.CreateStandard();
        var checkIn = new DateOnly(2024, 6, 3).ToDateTime(new TimeOnly(8, 1));

        var (status, lateMinutes, _, _) = await _sut.EvaluateCheckIn(checkIn, schedule);

        status.Should().Be(AttendanceStatus.Late);
        lateMinutes.Should().Be(1);
    }

    [Fact]
    public void CalculateBreakDuration_FullShift_DeductsOneHour()
    {
        var schedule = WorkSchedule.CreateStandard();

        _sut.CalculateBreakDuration(TimeSpan.FromHours(9), schedule)
            .Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void CalculateBreakDuration_ShiftOverFourHours_DeductsConfiguredOneHour()
    {
        var schedule = WorkSchedule.CreateStandard();

        _sut.CalculateBreakDuration(TimeSpan.FromHours(5), schedule)
            .Should().Be(TimeSpan.FromHours(1));
    }

    [Fact]
    public void CalculateOvertimeHours_AfterFivePm_CalculatesHoursAfterBreakAndStandardDay()
    {
        var schedule = WorkSchedule.CreateStandard();
        var checkIn = new DateTime(2024, 6, 3, 8, 0, 0);
        var checkOut = new DateTime(2024, 6, 3, 17, 30, 0);
        var workDuration = checkOut - checkIn;
        var overtime = _sut.CalculateOvertimeHours(
            workDuration,
            _sut.CalculateBreakDuration(workDuration, schedule),
            schedule,
            isWeekend: false,
            isHoliday: false,
            checkIn,
            checkOut);

        overtime.Should().Be(0.5m);
    }

    [Fact]
    public void CalculateOvertimeHours_EarlyCheckInDoesNotCreateOvertimeBeforeShiftEnd()
    {
        var schedule = WorkSchedule.CreateStandard();
        var checkIn = new DateTime(2024, 6, 3, 7, 0, 0);
        var checkOut = new DateTime(2024, 6, 3, 16, 30, 0);
        var workDuration = checkOut - checkIn;

        var overtime = _sut.CalculateOvertimeHours(
            workDuration,
            _sut.CalculateBreakDuration(workDuration, schedule),
            schedule,
            isWeekend: false,
            isHoliday: false,
            checkIn,
            checkOut);

        overtime.Should().Be(0);
    }

    [Fact]
    public void AttendanceTimeZone_ConvertsUtcToUlaanbaatarWallClockTime()
    {
        var utc = new DateTime(2024, 6, 2, 16, 0, 0, DateTimeKind.Utc);

        var local = AttendanceTimeZone.ToLocalTime(utc);

        local.Should().Be(new DateTime(2024, 6, 3, 0, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public async Task EvaluateCheckIn_FourHoursLate_IsHalfDayAndBeyondReviewThreshold()
    {
        var schedule = WorkSchedule.CreateStandard();
        var checkIn = new DateOnly(2024, 6, 3).ToDateTime(new TimeOnly(12, 0));

        var (status, lateMinutes, _, isHalfDay) = await _sut.EvaluateCheckIn(checkIn, schedule);

        status.Should().Be(AttendanceStatus.HalfDay);
        lateMinutes.Should().Be(240);
        isHalfDay.Should().BeTrue();
    }
}
