using System.Security.Claims;
using System.Reflection;
using AttendanceSystem.API.Controllers;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.IntegrationTests;

public sealed class NotificationsApiControllerTests : IDisposable
{
    private static readonly Guid EmployeeId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherEmployeeId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly ApplicationDbContext _db = TestDatabase.CreateContext();
    private readonly NotificationsApiController _controller;

    public NotificationsApiControllerTests()
    {
        _controller = new NotificationsApiController(_db);
        SetEmployee(EmployeeId);
    }

    [Fact]
    public async Task List_ReturnsOnlyGlobalAndCurrentEmployeeNotifications()
    {
        _db.Notifications.AddRange(
            CreateNotification(EmployeeId, "Own unread", false),
            CreateNotification(EmployeeId, "Own read", true),
            CreateNotification(OtherEmployeeId, "Other employee", false),
            CreateNotification(null, "Global unread", false));
        await _db.SaveChangesAsync();

        var action = await _controller.List(isRead: false);

        var response = Assert.IsType<PagedResponseDto<NotificationApiDto>>(
            Assert.IsType<OkObjectResult>(action.Result).Value);
        Assert.Equal(2, response.TotalCount);
        Assert.Equal(
            new[] { "Global unread", "Own unread" },
            response.Items.Select(notification => notification.Title).OrderBy(title => title));
        Assert.All(response.Items, notification => Assert.False(notification.IsRead));
    }

    [Fact]
    public async Task List_ClampsPagingValuesToSupportedMinimums()
    {
        _db.Notifications.AddRange(
            CreateNotification(EmployeeId, "First", false),
            CreateNotification(EmployeeId, "Second", false));
        await _db.SaveChangesAsync();

        var action = await _controller.List(pageNumber: 0, pageSize: 0);

        var response = Assert.IsType<PagedResponseDto<NotificationApiDto>>(
            Assert.IsType<OkObjectResult>(action.Result).Value);
        Assert.Equal(1, response.PageNumber);
        Assert.Equal(1, response.PageSize);
        Assert.Equal(2, response.TotalCount);
        Assert.Single(response.Items);
    }

    [Fact]
    public async Task MarkRead_AllowsOwnAndGlobalNotificationsButNotOtherEmployees()
    {
        var own = CreateNotification(EmployeeId, "Own", false);
        var global = CreateNotification(null, "Global", false);
        var other = CreateNotification(OtherEmployeeId, "Other", false);
        _db.Notifications.AddRange(own, global, other);
        await _db.SaveChangesAsync();

        Assert.IsType<OkObjectResult>(await _controller.MarkRead(own.Id, CancellationToken.None));
        Assert.IsType<OkObjectResult>(await _controller.MarkRead(global.Id, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await _controller.MarkRead(other.Id, CancellationToken.None));

        Assert.True((await _db.Notifications.SingleAsync(notification => notification.Id == own.Id)).IsRead);
        Assert.True((await _db.Notifications.SingleAsync(notification => notification.Id == global.Id)).IsRead);
        Assert.False((await _db.Notifications.SingleAsync(notification => notification.Id == other.Id)).IsRead);
    }

    [Fact]
    public async Task UnreadCount_ExcludesOtherEmployeesAndReadNotifications()
    {
        _db.Notifications.AddRange(
            CreateNotification(EmployeeId, "Own unread", false),
            CreateNotification(EmployeeId, "Own read", true),
            CreateNotification(OtherEmployeeId, "Other unread", false),
            CreateNotification(null, "Global unread", false));
        await _db.SaveChangesAsync();

        var action = await _controller.UnreadCount(CancellationToken.None);

        var response = Assert.IsType<UnreadNotificationCountResponseDto>(
            Assert.IsType<OkObjectResult>(action.Result).Value);
        Assert.Equal(2, response.Count);
    }

    [Fact]
    public void Controller_RequiresAuthentication()
    {
        Assert.NotNull(typeof(NotificationsApiController).GetCustomAttribute<AuthorizeAttribute>());
    }

    private static Notification CreateNotification(Guid? employeeId, string title, bool isRead)
    {
        var notification = Notification.Create(employeeId, title, "Test message", NotificationChannel.InApp);
        if (isRead)
            notification.MarkRead();
        return notification;
    }

    private void SetEmployee(Guid employeeId)
    {
        var identity = new ClaimsIdentity(
            [new Claim("employee_id", employeeId.ToString())],
            "test");
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    public void Dispose() => _db.Dispose();
}
