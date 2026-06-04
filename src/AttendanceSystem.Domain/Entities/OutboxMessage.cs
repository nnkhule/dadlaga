using AttendanceSystem.Domain.Common;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Outbox entry for reliable async notification processing.
/// </summary>
public class OutboxMessage : BaseEntity
{
    public string EventType { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime? ProcessedAt { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(string eventType, string payload)
        => new() { EventType = eventType, Payload = payload };

    public void MarkProcessed() { ProcessedAt = DateTime.UtcNow; SetUpdated(); }

    public void MarkFailed(string error)
    {
        Error = error;
        RetryCount++;
        SetUpdated();
    }
}
