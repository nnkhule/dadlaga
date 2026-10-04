using System.Security.Claims;
using AttendanceSystem.API.Controllers;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.FileProviders;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.IntegrationTests;

public sealed class LeaveControllerTests : IDisposable
{
    private static readonly Guid EmployeeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly ApplicationDbContext _db = TestDatabase.CreateContext();
    private readonly LeaveController _controller;

    public LeaveControllerTests()
    {
        _controller = new LeaveController(_db, new TestWebHostEnvironment());
    }

    [Fact]
    public async Task CreateLeave_CreatesValidHourlyRequest()
    {
        AddUser("Employee", EmployeeId);
        _db.LeavePolicies.Add(LeavePolicy.Create(
            LeaveType.Other, isPaid: false, deductsBalance: false, annualAllowanceDays: null,
            advanceNoticeDays: 0, requiresDocument: false, requiresReason: true, adminOnly: false));
        await _db.SaveChangesAsync();

        var day = DateOnly.FromDateTime(DateTime.Today.AddDays(2));
        var result = await _controller.CreateLeave(new LeaveController.LeaveRequestDto
        {
            StartDate = day.ToString("yyyy-MM-dd"),
            EndDate = day.ToString("yyyy-MM-dd"),
            Type = "Other",
            Reason = "Doctor appointment",
            LeaveMode = "Hourly",
            StartTime = "09:00",
            EndTime = "12:30"
        }, null, CancellationToken.None);

        Assert.IsType<CreatedResult>(result);
        var saved = await _db.LeaveRequests.SingleAsync();
        Assert.Equal(EmployeeId, saved.EmployeeId);
        Assert.Equal("Hourly", saved.LeaveMode);
        Assert.Equal(3.5m, saved.Hours);
        Assert.Equal(new TimeOnly(9, 0), saved.StartTime);
        Assert.Equal(RequestStatus.Pending, saved.Status);
    }

    [Theory]
    [InlineData("08:00", "08:00")]
    [InlineData("13:00", "12:00")]
    [InlineData("08:00", "17:00")]
    public async Task CreateLeave_RejectsInvalidHourlyTimeRange(string startTime, string endTime)
    {
        AddUser("Employee", EmployeeId);
        _db.LeavePolicies.Add(LeavePolicy.Create(
            LeaveType.Other, isPaid: false, deductsBalance: false, annualAllowanceDays: null,
            advanceNoticeDays: 0, requiresDocument: false, requiresReason: true, adminOnly: false));
        await _db.SaveChangesAsync();

        var day = DateOnly.FromDateTime(DateTime.Today.AddDays(2));
        var result = await _controller.CreateLeave(new LeaveController.LeaveRequestDto
        {
            StartDate = day.ToString("yyyy-MM-dd"),
            EndDate = day.ToString("yyyy-MM-dd"),
            Type = "Other",
            Reason = "Doctor appointment",
            LeaveMode = "Hourly",
            StartTime = startTime,
            EndTime = endTime
        }, null, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(await _db.LeaveRequests.ToListAsync());
    }

    [Fact]
    public async Task Approve_ApprovesPendingRequestAndNotifiesEmployee()
    {
        AddUser("SuperAdmin", null);
        var leave = AddLeave(RequestStatus.Pending);
        await _db.SaveChangesAsync();

        var result = await _controller.Approve(leave.Id, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var saved = await _db.LeaveRequests.SingleAsync();
        Assert.Equal(RequestStatus.Approved, saved.Status);
        Assert.Equal(Guid.Empty, saved.ApprovedBy);
        Assert.Equal(leave.Id, (await _db.Notifications.SingleAsync()).RelatedEntityId);
    }

    [Fact]
    public async Task Approve_ReturnsNotFoundOrRejectsNonPendingRequests()
    {
        var notFound = await _controller.Approve(Guid.NewGuid(), CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(notFound);

        var leave = AddLeave(RequestStatus.Approved);
        await _db.SaveChangesAsync();
        var alreadyApproved = await _controller.Approve(leave.Id, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(alreadyApproved);
        Assert.Empty(await _db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task Reject_RequiresReasonAndPersistsTrimmedDecision()
    {
        AddUser("HRManager", null);
        var leave = AddLeave(RequestStatus.Pending);
        await _db.SaveChangesAsync();

        var missingReason = await _controller.Reject(leave.Id, new LeaveController.DecisionRequest("  "), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(missingReason);
        Assert.Equal(RequestStatus.Pending, (await _db.LeaveRequests.SingleAsync()).Status);

        var rejected = await _controller.Reject(leave.Id, new LeaveController.DecisionRequest("  Not enough details  "), CancellationToken.None);

        Assert.IsType<OkObjectResult>(rejected);
        var saved = await _db.LeaveRequests.SingleAsync();
        Assert.Equal(RequestStatus.Rejected, saved.Status);
        Assert.Equal("Not enough details", saved.DecisionReason);
        Assert.Equal("Not enough details", (await _db.Notifications.SingleAsync()).Body);
    }

    [Fact]
    public async Task Cancel_OnlyCancelsTheCurrentEmployeesPendingRequest()
    {
        AddUser("Employee", EmployeeId);
        var ownRequest = AddLeave(RequestStatus.Pending, EmployeeId);
        var otherRequest = AddLeave(RequestStatus.Pending, Guid.NewGuid());
        await _db.SaveChangesAsync();

        var forbiddenByOwnership = await _controller.Cancel(otherRequest.Id, CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(forbiddenByOwnership);

        var cancelled = await _controller.Cancel(ownRequest.Id, CancellationToken.None);

        Assert.IsType<OkObjectResult>(cancelled);
        Assert.Equal(RequestStatus.Cancelled,
            (await _db.LeaveRequests.SingleAsync(request => request.Id == ownRequest.Id)).Status);
        Assert.Equal(RequestStatus.Pending,
            (await _db.LeaveRequests.SingleAsync(request => request.Id == otherRequest.Id)).Status);
    }

    [Fact]
    public async Task Cancel_RejectsRequestsThatAreNotPending()
    {
        AddUser("Employee", EmployeeId);
        var leave = AddLeave(RequestStatus.Approved, EmployeeId);
        await _db.SaveChangesAsync();

        var result = await _controller.Cancel(leave.Id, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(RequestStatus.Approved, (await _db.LeaveRequests.SingleAsync()).Status);
    }

    private LeaveRequest AddLeave(RequestStatus status, Guid? employeeId = null)
    {
        var leave = LeaveRequest.Create(
            employeeId ?? EmployeeId,
            LeaveType.Other,
            DateOnly.FromDateTime(DateTime.Today.AddDays(5)),
            DateOnly.FromDateTime(DateTime.Today.AddDays(5)),
            "Test reason");
        if (status == RequestStatus.Approved)
            leave.Approve(Guid.NewGuid());
        else if (status == RequestStatus.Rejected)
            leave.Reject(Guid.NewGuid(), "Rejected");
        else if (status == RequestStatus.Cancelled)
            leave.Cancel();

        _db.LeaveRequests.Add(leave);
        return leave;
    }

    private void AddUser(string role, Guid? employeeId)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        if (employeeId.HasValue)
            claims.Add(new Claim("employee_id", employeeId.Value.ToString()));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    claims, "test", ClaimTypes.Name, ClaimTypes.Role))
            }
        };
    }

    public void Dispose() => _db.Dispose();

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "AttendanceSystem.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = Path.GetTempPath();
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
