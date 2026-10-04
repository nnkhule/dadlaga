using System.ComponentModel.DataAnnotations;
using System.Reflection;
using AttendanceSystem.API.Controllers;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.IntegrationTests;

public sealed class DepartmentsApiControllerTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly DepartmentsApiController _controller;

    public DepartmentsApiControllerTests()
    {
        _db = TestDatabase.CreateContext();
        _controller = new DepartmentsApiController(_db);
    }

    [Fact]
    public async Task List_FiltersAndPaginatesActiveDepartments()
    {
        var matching = Department.Create("Engineering");
        var other = Department.Create("Finance");
        var inactive = Department.Create("Engineering Archive");
        inactive.Deactivate();
        _db.Departments.AddRange(matching, other, inactive);
        await _db.SaveChangesAsync();

        var action = await _controller.List(pageNumber: 0, pageSize: 1, search: " Engineering ");

        var response = Assert.IsType<PagedResponseDto<DepartmentApiDto>>(
            Assert.IsType<OkObjectResult>(action.Result).Value);
        Assert.Equal(1, response.PageNumber);
        Assert.Equal(1, response.PageSize);
        Assert.Equal(1, response.TotalCount);
        Assert.Equal("Engineering", Assert.Single(response.Items).Name);
    }

    [Fact]
    public async Task List_CanIncludeInactiveDepartments()
    {
        var active = Department.Create("Active");
        var inactive = Department.Create("Inactive");
        inactive.Deactivate();
        _db.Departments.AddRange(active, inactive);
        await _db.SaveChangesAsync();

        var action = await _controller.List(includeInactive: true);

        var response = Assert.IsType<PagedResponseDto<DepartmentApiDto>>(
            Assert.IsType<OkObjectResult>(action.Result).Value);
        Assert.Equal(2, response.TotalCount);
    }

    [Fact]
    public async Task Details_ReturnsNotFoundForUnknownDepartment()
    {
        var action = await _controller.Details(Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(action.Result);
    }

    [Fact]
    public async Task Create_TrimsNameAndReturnsCreatedResource()
    {
        var action = await _controller.Create(new DepartmentFormApiDto("  Operations  ", null), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(action.Result);
        var response = Assert.IsType<DepartmentApiDto>(created.Value);
        Assert.Equal("Operations", response.Name);
        Assert.True(response.IsActive);
        Assert.Equal("Operations", await _db.Departments.Select(d => d.Name).SingleAsync());
    }

    [Fact]
    public async Task Create_RejectsBlankName()
    {
        var action = await _controller.Create(new DepartmentFormApiDto("  ", null), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Empty(await _db.Departments.ToListAsync());
    }

    [Fact]
    public async Task Update_ChangesExistingDepartmentAndRejectsMissingId()
    {
        var department = Department.Create("Old name");
        _db.Departments.Add(department);
        await _db.SaveChangesAsync();

        var updated = await _controller.Update(department.Id, new DepartmentFormApiDto("  New name ", null), CancellationToken.None);
        var response = Assert.IsType<DepartmentApiDto>(Assert.IsType<OkObjectResult>(updated.Result).Value);
        Assert.Equal("New name", response.Name);

        var missing = await _controller.Update(Guid.NewGuid(), new DepartmentFormApiDto("Missing", null), CancellationToken.None);
        Assert.IsType<NotFoundResult>(missing.Result);
    }

    [Fact]
    public async Task DeactivateAndActivate_ChangeDepartmentState()
    {
        var department = Department.Create("Operations");
        _db.Departments.Add(department);
        await _db.SaveChangesAsync();

        Assert.IsType<NoContentResult>(await _controller.Deactivate(department.Id, CancellationToken.None));
        Assert.False((await _db.Departments.SingleAsync()).IsActive);
        Assert.IsType<NoContentResult>(await _controller.Activate(department.Id, CancellationToken.None));
        Assert.True((await _db.Departments.SingleAsync()).IsActive);

        Assert.IsType<NotFoundResult>(await _controller.Deactivate(Guid.NewGuid(), CancellationToken.None));
        Assert.IsType<NotFoundResult>(await _controller.Activate(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public void Controller_RequiresAuthentication()
    {
        Assert.NotNull(typeof(DepartmentsApiController).GetCustomAttribute<AuthorizeAttribute>());
    }

    public void Dispose() => _db.Dispose();
}

public sealed class SettingsApiControllerTests : IDisposable
{
    private readonly ApplicationDbContext _db;
    private readonly SettingsApiController _controller;

    public SettingsApiControllerTests()
    {
        _db = TestDatabase.CreateContext();
        _controller = new SettingsApiController(_db);
    }

    [Fact]
    public async Task ReadEndpoints_ReturnStoredScheduleAndActiveOfficeSettings()
    {
        var schedule = WorkSchedule.CreateStandard("Standard");
        var activeOffice = OfficeLocation.Create("HQ", 47.92, 106.91, 250);
        var inactiveOffice = OfficeLocation.Create("Old office", 0, 0, 100);
        inactiveOffice.Update("Old office", 0, 0, 100, false);
        _db.WorkSchedules.Add(schedule);
        _db.OfficeLocations.AddRange(activeOffice, inactiveOffice);
        await _db.SaveChangesAsync();

        var rules = Assert.IsType<AttendanceRulesApiDto>(Assert.IsType<OkObjectResult>(
            (await _controller.AttendanceRules(CancellationToken.None)).Result).Value);
        Assert.Equal(0, rules.GraceMinutes);
        var workSchedule = Assert.IsType<WorkScheduleSettingsApiDto>(Assert.IsType<OkObjectResult>(
            (await _controller.WorkSchedule(CancellationToken.None)).Result).Value);
        Assert.Equal("Standard", workSchedule.Name);
        Assert.Equal(new TimeOnly(8, 0), workSchedule.ShiftStart);

        var gps = Assert.IsType<GpsSettingsApiDto>(Assert.IsType<OkObjectResult>(
            (await _controller.Gps(CancellationToken.None)).Result).Value);
        Assert.True(gps.Enabled);
        Assert.Equal(47.92, gps.OfficeLatitude);
        Assert.Equal(250, gps.AllowedRadiusMeters);

        var offices = Assert.IsAssignableFrom<IReadOnlyList<OfficeLocationSettingsApiDto>>(
            Assert.IsType<OkObjectResult>((await _controller.OfficeLocations(CancellationToken.None)).Result).Value);
        Assert.Equal(2, offices.Count);
        Assert.Contains(offices, office => office.Name == "Old office" && !office.IsActive);
    }

    [Fact]
    public async Task ReadEndpoints_ReturnSafeDefaultsWhenNoSettingsExist()
    {
        var company = Assert.IsType<CompanySettingsApiDto>(Assert.IsType<OkObjectResult>(_controller.Company().Result).Value);
        Assert.Null(company.CompanyName);

        var rules = Assert.IsType<AttendanceRulesApiDto>(Assert.IsType<OkObjectResult>(
            (await _controller.AttendanceRules(CancellationToken.None)).Result).Value);
        Assert.Equal(0, rules.GraceMinutes);
        var schedule = await _controller.WorkSchedule(CancellationToken.None);
        Assert.Null(Assert.IsType<OkObjectResult>(schedule.Result).Value);
        var gps = Assert.IsType<GpsSettingsApiDto>(Assert.IsType<OkObjectResult>(
            (await _controller.Gps(CancellationToken.None)).Result).Value);
        Assert.False(gps.Enabled);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<OfficeLocationSettingsApiDto>>(
            Assert.IsType<OkObjectResult>((await _controller.OfficeLocations(CancellationToken.None)).Result).Value));
    }

    [Fact]
    public async Task CreateAndUpdateOfficeLocation_PersistChanges()
    {
        var created = await _controller.CreateOfficeLocation(
            new OfficeLocationRequest("Branch", 35, 100, 400, true), CancellationToken.None);
        var createdDto = Assert.IsType<OfficeLocationSettingsApiDto>(Assert.IsType<OkObjectResult>(created.Result).Value);
        Assert.Equal("Branch", createdDto.Name);
        Assert.Equal(400, createdDto.RadiusMeters);

        var updated = await _controller.UpdateOfficeLocation(
            createdDto.Id, new OfficeLocationRequest("New branch", -35, -100, 600, false), CancellationToken.None);
        var updatedDto = Assert.IsType<OfficeLocationSettingsApiDto>(Assert.IsType<OkObjectResult>(updated.Result).Value);
        Assert.Equal("New branch", updatedDto.Name);
        Assert.False(updatedDto.IsActive);

        Assert.IsType<NotFoundResult>((
            await _controller.UpdateOfficeLocation(Guid.NewGuid(), new OfficeLocationRequest("Missing", 0, 0, 1, true), CancellationToken.None)).Result);
    }

    [Fact]
    public void OfficeLocationRequest_RejectsInvalidNameCoordinatesAndRadius()
    {
        var parameters = typeof(OfficeLocationRequest).GetConstructors().Single().GetParameters()
            .ToDictionary(parameter => parameter.Name!, parameter => parameter);

        AssertInvalidParameter(parameters[nameof(OfficeLocationRequest.Name)], " ");
        AssertInvalidParameter(parameters[nameof(OfficeLocationRequest.Latitude)], 91d);
        AssertInvalidParameter(parameters[nameof(OfficeLocationRequest.Longitude)], -181d);
        AssertInvalidParameter(parameters[nameof(OfficeLocationRequest.RadiusMeters)], 0);
    }

    private static void AssertInvalidParameter(ParameterInfo parameter, object value)
    {
        var attributes = parameter.GetCustomAttributes<ValidationAttribute>().ToArray();
        Assert.NotEmpty(attributes);
        Assert.Contains(attributes, attribute => !attribute.IsValid(value));
    }

    [Fact]
    public void ControllerAndMutatingActions_RequireAuthenticationAndAdminRole()
    {
        Assert.NotNull(typeof(SettingsApiController).GetCustomAttribute<AuthorizeAttribute>());
        foreach (var actionName in new[] { nameof(SettingsApiController.CreateOfficeLocation), nameof(SettingsApiController.UpdateOfficeLocation) })
        {
            var method = typeof(SettingsApiController).GetMethod(actionName)!;
            Assert.Contains(method.GetCustomAttributes<AuthorizeAttribute>(), attribute => attribute.Roles == "SuperAdmin,HRManager");
        }
    }

    public void Dispose() => _db.Dispose();
}

public sealed class AuthorizationMetadataTests
{
    [Fact]
    public void AuthenticationEndpoints_AreAnonymousButAccountAdministrationRequiresAdminRole()
    {
        Assert.NotNull(GetMethod<AuthController>(nameof(AuthController.Login)).GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(GetMethod<AuthController>(nameof(AuthController.Refresh)).GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.Contains(GetMethod<AuthController>(nameof(AuthController.SetupEmployeeAccount)).GetCustomAttributes<AuthorizeAttribute>(),
            attribute => attribute.Roles == "SuperAdmin,HRManager");
        Assert.Contains(GetMethod<AuthController>(nameof(AuthController.AdminResetPassword)).GetCustomAttributes<AuthorizeAttribute>(),
            attribute => attribute.Roles == "SuperAdmin,HRManager");
    }

    private static MethodInfo GetMethod<TController>(string methodName) =>
        typeof(TController).GetMethod(methodName) ?? throw new InvalidOperationException($"Missing controller method {methodName}.");
}

internal static class TestDatabase
{
    public static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"integration-tests-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
