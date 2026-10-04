using System.Reflection;
using AttendanceSystem.Application.Common;
using AttendanceSystem.Application.Configuration;
using AttendanceSystem.Application.Features.Attendance.Commands.CheckOut;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Application.Interfaces.Repositories;
using AttendanceSystem.Application.Services;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;

namespace AttendanceSystem.UnitTests.Features.Attendance.Commands;

public class CheckOutCommandHandlerTests
{
    private readonly Mock<IAttendanceRepository> _attendanceRepository = new();
    private readonly Mock<IEmployeeRepository> _employeeRepository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IGeofenceService> _geofenceService = new();
    private readonly Mock<IHolidayRepository> _holidayRepository = new();
    private readonly Mock<IClock> _clock = new();
    private readonly DateTime _now = new(2024, 6, 3, 17, 0, 0, DateTimeKind.Unspecified);
    private readonly DateOnly _today = new(2024, 6, 3);

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenEmployeeIsActiveAndGpsIsWithinRadius()
    {
        var employee = CreateActiveEmployee();
        var record = AttendanceRecord.CreateCheckIn(
            employee.Id,
            _now.AddHours(-9),
            AttendanceStatus.Present,
            0m,
            VerificationMethod.Gps,
            false,
            false,
            false,
            employee.OfficeLocation!.Latitude,
            employee.OfficeLocation.Longitude,
            null,
            null);

        var handler = CreateHandler();
        _employeeRepository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        _attendanceRepository.Setup(r => r.GetTodayRecordAsync(employee.Id, _today, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _geofenceService.Setup(g => g.IsWithinRadius(
                employee.OfficeLocation.Latitude,
                employee.OfficeLocation.Longitude,
                employee.OfficeLocation.Latitude,
                employee.OfficeLocation.Longitude,
                employee.OfficeLocation.RadiusMeters)).Returns(true);
        _holidayRepository.Setup(h => h.IsHolidayAsync(_today, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var result = await handler.Handle(
            new CheckOutCommand(employee.Id, employee.OfficeLocation.Latitude, employee.OfficeLocation.Longitude, "Gps"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Status.Should().Be(AttendanceStatus.Present);
        result.Value.CheckOutTime.Should().NotBeNull();
        record.CheckOutTime.Should().Be(_now);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_Fails_WhenEmployeeIsMissingOrInactive(bool shouldReturnNull)
    {
        var employee = CreateActiveEmployee();
        if (!shouldReturnNull)
        {
            employee.Deactivate();
        }

        var handler = CreateHandler();
        _employeeRepository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shouldReturnNull ? null : employee);

        var result = await handler.Handle(
            new CheckOutCommand(employee.Id, 47.912216, 106.931346, "Gps"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("EMPLOYEE_NOT_FOUND");
    }

    [Fact]
    public async Task Handle_Fails_WhenEmployeeSetupIsMissing()
    {
        var employee = CreateActiveEmployee();
        SetProperty<WorkSchedule?>(employee, nameof(Employee.WorkSchedule), null);
        SetProperty<OfficeLocation?>(employee, nameof(Employee.OfficeLocation), null);
        var handler = CreateHandler();

        _employeeRepository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);

        var result = await handler.Handle(
            new CheckOutCommand(employee.Id, 47.912216, 106.931346, "Gps"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("CONFIG_MISSING");
    }

    [Fact]
    public async Task Handle_Fails_WhenNoCheckInExistsForToday()
    {
        var employee = CreateActiveEmployee();
        var handler = CreateHandler();

        _employeeRepository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        _attendanceRepository.Setup(r => r.GetTodayRecordAsync(employee.Id, _today, It.IsAny<CancellationToken>())).ReturnsAsync((AttendanceRecord?)null);

        var result = await handler.Handle(
            new CheckOutCommand(employee.Id, employee.OfficeLocation!.Latitude, employee.OfficeLocation.Longitude, "Gps"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_CHECKIN");
    }

    [Fact]
    public async Task Handle_Fails_WhenEmployeeAlreadyCheckedOut()
    {
        var employee = CreateActiveEmployee();
        var record = AttendanceRecord.CreateCheckIn(
            employee.Id,
            _now.AddHours(-9),
            AttendanceStatus.Present,
            0m,
            VerificationMethod.Gps,
            false,
            false,
            false,
            employee.OfficeLocation!.Latitude,
            employee.OfficeLocation.Longitude,
            null,
            null);
        record.CheckOut(_now, AttendanceStatus.Present, TimeSpan.FromMinutes(60), 1m, 0m, VerificationMethod.Gps, employee.OfficeLocation.Latitude, employee.OfficeLocation.Longitude, null);

        var handler = CreateHandler();
        _employeeRepository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        _attendanceRepository.Setup(r => r.GetTodayRecordAsync(employee.Id, _today, It.IsAny<CancellationToken>())).ReturnsAsync(record);

        var result = await handler.Handle(
            new CheckOutCommand(employee.Id, employee.OfficeLocation.Latitude, employee.OfficeLocation.Longitude, "Gps"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("ALREADY_CHECKED_OUT");
    }

    [Fact]
    public async Task Handle_Fails_WhenGpsIsRequiredButMissing()
    {
        var employee = CreateActiveEmployee();
        var record = AttendanceRecord.CreateCheckIn(
            employee.Id,
            _now.AddHours(-9),
            AttendanceStatus.Present,
            0m,
            VerificationMethod.Gps,
            false,
            false,
            false,
            employee.OfficeLocation!.Latitude,
            employee.OfficeLocation.Longitude,
            null,
            null);

        var handler = CreateHandler(new AttendanceRulesOptions { RequireGpsForCheckOut = true });
        _employeeRepository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        _attendanceRepository.Setup(r => r.GetTodayRecordAsync(employee.Id, _today, It.IsAny<CancellationToken>())).ReturnsAsync(record);

        var result = await handler.Handle(
            new CheckOutCommand(employee.Id, null, null, "Gps"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("GPS_REQUIRED");
    }

    [Fact]
    public async Task Handle_Fails_WhenGpsIsOutsideRadiusAndRequired()
    {
        var employee = CreateActiveEmployee();
        var record = AttendanceRecord.CreateCheckIn(
            employee.Id,
            _now.AddHours(-9),
            AttendanceStatus.Present,
            0m,
            VerificationMethod.Gps,
            false,
            false,
            false,
            employee.OfficeLocation!.Latitude,
            employee.OfficeLocation.Longitude,
            null,
            null);

        var handler = CreateHandler(new AttendanceRulesOptions { RequireGpsForCheckOut = true });
        _employeeRepository.Setup(r => r.GetByIdAsync(employee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(employee);
        _attendanceRepository.Setup(r => r.GetTodayRecordAsync(employee.Id, _today, It.IsAny<CancellationToken>())).ReturnsAsync(record);
        _geofenceService.Setup(g => g.IsWithinRadius(20, 30, employee.OfficeLocation.Latitude, employee.OfficeLocation.Longitude, employee.OfficeLocation.RadiusMeters)).Returns(false);
        _geofenceService.Setup(g => g.CalculateDistanceMeters(20, 30, employee.OfficeLocation.Latitude, employee.OfficeLocation.Longitude)).Returns(5000d);

        var result = await handler.Handle(
            new CheckOutCommand(employee.Id, 20, 30, "Gps"),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("OUT_OF_RANGE");
    }

    private CheckOutCommandHandler CreateHandler(AttendanceRulesOptions? options = null)
    {
        var actualOptions = options ?? new AttendanceRulesOptions
        {
            RequireGpsForCheckOut = true,
            DefaultGraceMinutes = 10,
            EarlyCheckinThresholdMinutes = 120,
            HalfDayLateThresholdMinutes = 180
        };

        _clock.Setup(c => c.TodayLocal).Returns(_today);
        _clock.Setup(c => c.LocalNow).Returns(_now);
        _holidayRepository.Setup(h => h.IsHolidayAsync(_today, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var attendanceRules = new AttendanceRulesService(Options.Create(actualOptions), _holidayRepository.Object);
        return new CheckOutCommandHandler(
            _attendanceRepository.Object,
            _employeeRepository.Object,
            _unitOfWork.Object,
            _geofenceService.Object,
            attendanceRules,
            Options.Create(actualOptions),
            _clock.Object,
            _holidayRepository.Object);
    }

    private static Employee CreateActiveEmployee(WorkSchedule? schedule = null, OfficeLocation? office = null)
    {
        var employee = Employee.Create(
            "EMP-100",
            "Test Employee",
            "test@attendance.local",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DateOnly(2024, 1, 1),
            ContractType.FullTime);

        SetProperty(employee, nameof(Employee.WorkSchedule), schedule ?? WorkSchedule.CreateStandard());
        SetProperty(employee, nameof(Employee.OfficeLocation), office ?? OfficeLocation.Create("Head Office", 47.912216, 106.931346, 5000));
        return employee;
    }

    private static void SetProperty<T>(object target, string propertyName, T value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        property.Should().NotBeNull();
        property!.SetValue(target, value);
    }
}



