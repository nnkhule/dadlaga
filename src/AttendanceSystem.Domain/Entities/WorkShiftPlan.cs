using AttendanceSystem.Domain.Common;
using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Represents a work shift plan for an employee.
/// </summary>
public class WorkShiftPlan : BaseEntity
{
    public Guid EmployeeId { get; private set; }
    public Guid WorkScheduleId { get; private set; }
    public DateOnly PlanDate { get; private set; }
    public ShiftPlanStatus Status { get; private set; } = ShiftPlanStatus.Draft;
    public string? Notes { get; private set; }

    // Navigation properties
    public Employee? Employee { get; private set; }
    public WorkSchedule? WorkSchedule { get; private set; }
    public ICollection<TaskItem> TaskItems { get; private set; } = new List<TaskItem>();

    private WorkShiftPlan() { }

    public static WorkShiftPlan Create(
        Guid employeeId,
        Guid workScheduleId,
        DateOnly planDate,
        string? notes = null)
    {
        return new WorkShiftPlan
        {
            EmployeeId = employeeId,
            WorkScheduleId = workScheduleId,
            PlanDate = planDate,
            Notes = notes
        };
    }

    public void UpdateStatus(ShiftPlanStatus status)
    {
        Status = status;
        SetUpdated();
    }

    public void UpdateNotes(string? notes)
    {
        Notes = notes;
        SetUpdated();
    }
}
