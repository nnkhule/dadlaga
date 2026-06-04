using AttendanceSystem.Domain.Common;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Immutable audit trail for data changes.
/// </summary>
public class AuditLog : BaseEntity
{
    public string EntityName { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? OldValues { get; private set; }
    public string? NewValues { get; private set; }
    public string? PerformedBy { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    public static AuditLog Create(string entityName, Guid entityId, string action,
        string? oldValues, string? newValues, string? performedBy, string? ip, string? userAgent)
        => new()
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            OldValues = oldValues,
            NewValues = newValues,
            PerformedBy = performedBy,
            IpAddress = ip,
            UserAgent = userAgent
        };
}
