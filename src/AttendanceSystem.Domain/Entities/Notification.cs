using AttendanceSystem.Domain.Common;
using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Stored notification for in-app history and multi-channel delivery tracking.
/// </summary>
public class Notification : BaseEntity
{
    public Guid? EmployeeId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public NotificationChannel Channel { get; private set; }
    public bool IsRead { get; private set; }
    public Guid? RelatedEntityId { get; private set; }
    public string? RelatedEntityType { get; private set; }

    private Notification() { }

    public static Notification Create(Guid? employeeId, string title, string body,
        NotificationChannel channel, Guid? relatedEntityId = null, string? relatedEntityType = null)
        => new()
        {
            EmployeeId = employeeId,
            Title = title,
            Body = body,
            Channel = channel,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType
        };

    public void MarkRead() { IsRead = true; SetUpdated(); }
}
