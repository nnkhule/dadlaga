using AttendanceSystem.Domain.Common;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Represents a comment on a task item.
/// </summary>
public class TaskComment : BaseEntity
{
    public Guid TaskItemId { get; private set; }
    public string Content { get; private set; } = string.Empty;

    // Navigation properties
    public TaskItem? TaskItem { get; private set; }

    private TaskComment() { }

    public static TaskComment Create(Guid taskId, string content)
    {
        return new TaskComment
        {
            TaskItemId = taskId,
            Content = content
        };
    }

    public void UpdateContent(string content)
    {
        Content = content;
        SetUpdated();
    }
}