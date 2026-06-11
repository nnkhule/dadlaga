using System.Security.Claims;
using AttendanceSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public NotificationsApiController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<PagedResponseDto<NotificationApiDto>>> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? isRead = null,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var employeeId = GetEmployeeId();

        var query = _db.Notifications.AsNoTracking().Where(n => n.EmployeeId == null || n.EmployeeId == employeeId);
        if (isRead.HasValue)
            query = query.Where(n => n.IsRead == isRead.Value);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationApiDto(n.Id, n.Title, n.Body, n.CreatedAt, n.CreatedAt, n.IsRead))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponseDto<NotificationApiDto>(items, pageNumber, pageSize, total));
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var employeeId = GetEmployeeId();
        var notification = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && (n.EmployeeId == null || n.EmployeeId == employeeId), cancellationToken);
        if (notification is null)
            return NotFound();

        notification.MarkRead();
        await _db.SaveChangesAsync(cancellationToken);
        return Ok(new MessageResponseDto("Notification marked as read."));
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadNotificationCountResponseDto>> UnreadCount(CancellationToken cancellationToken)
    {
        var employeeId = GetEmployeeId();
        var count = await _db.Notifications.AsNoTracking().CountAsync(n => !n.IsRead && (n.EmployeeId == null || n.EmployeeId == employeeId), cancellationToken);
        return Ok(new UnreadNotificationCountResponseDto(count));
    }

    private Guid? GetEmployeeId()
    {
        var claim = User.FindFirstValue("employee_id");
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}

public sealed record NotificationApiDto(Guid Id, string Title, string Message, DateTime CreatedDate, DateTime CreatedAt, bool IsRead);
