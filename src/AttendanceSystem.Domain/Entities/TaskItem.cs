using AttendanceSystem.Domain.Common;
using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Represents a task item assigned to a work shift plan.
/// </summary>
public class TaskItem : BaseEntity
{
    public Guid WorkShiftPlanId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public TaskPriority Priority { get; private set; } = TaskPriority.Medium;
    public TaskItemStatus Status { get; private set; } = TaskItemStatus.Pending;
    public DateOnly? DueDate { get; private set; }
    public double? EstimatedHours { get; private set; }

    // Navigation properties
    public WorkShiftPlan? WorkShiftPlan { get; private set; }
    public ICollection<TaskComment> Comments { get; private set; } = new List<TaskComment>();

    private TaskItem() { }

    public static TaskItem Create(
        Guid workShiftPlanId,
        string title,
        string? description = null,
        TaskPriority priority = TaskPriority.Medium,
        DateOnly? dueDate = null,
        double? estimatedHours = null)
    {
        return new TaskItem
        {
            WorkShiftPlanId = workShiftPlanId,
            Title = title,
            Description = description,
            Priority = priority,
            DueDate = dueDate,
            EstimatedHours = estimatedHours
        };
    }

    public void UpdateDetails(
        string title,
        string? description = null,
        TaskPriority? priority = null,
        DateOnly? dueDate = null,
        double? estimatedHours = null)
    {
        Title = title;
        Description = description;
        if (priority.HasValue) Priority = priority.Value;
        if (dueDate.HasValue) DueDate = dueDate.Value;
        if (estimatedHours.HasValue) EstimatedHours = estimatedHours.Value;
        SetUpdated();
    }

    public void UpdateStatus(TaskItemStatus status)
    {
        Status = status;
        SetUpdated();
    }

    public void AddComment(TaskComment comment)
    {
        if (!Comments.Contains(comment))
            Comments.Add(comment);
    }
}