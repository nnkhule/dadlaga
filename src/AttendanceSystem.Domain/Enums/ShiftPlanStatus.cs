using System;

namespace AttendanceSystem.Domain.Enums;

/// <summary>
/// Status of a work shift plan.
/// </summary>
public enum ShiftPlanStatus
{
    /// <summary>
    /// The plan is drafted but not yet active.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// The plan is active and currently in effect.
    /// </summary>
    Active = 1,

    /// <summary>
    /// The plan has been completed or archived.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// The plan has been cancelled.
    /// </summary>
    Cancelled = 3
}