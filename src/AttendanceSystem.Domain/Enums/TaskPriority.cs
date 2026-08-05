using System;

namespace AttendanceSystem.Domain.Enums;

/// <summary>
/// Priority level of a task item.
/// </summary>
public enum TaskPriority
{
    /// <summary>
    /// Low priority task.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Medium priority task.
    /// </summary>
    Medium = 1,

    /// <summary>
    /// High priority task.
    /// </summary>
    High = 2,

    /// <summary>
    /// Urgent priority task.
    /// </summary>
    Urgent = 3
}