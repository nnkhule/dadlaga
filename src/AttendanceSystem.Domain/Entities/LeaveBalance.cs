using AttendanceSystem.Domain.Common;
using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Annual leave entitlement and usage for one employee and leave type.
/// </summary>
public sealed class LeaveBalance : BaseEntity
{
    public Guid EmployeeId { get; private set; }
    public LeaveType LeaveType { get; private set; }
    public int Year { get; private set; }
    public decimal AllocatedDays { get; private set; }
    public decimal UsedDays { get; private set; }
    public string? AdjustmentReason { get; private set; }

    public decimal RemainingDays => Math.Max(0, AllocatedDays - UsedDays);
    public Employee? Employee { get; private set; }

    private LeaveBalance() { }

    public static LeaveBalance Create(Guid employeeId, LeaveType leaveType, int year, decimal allocatedDays)
        => new()
        {
            EmployeeId = employeeId,
            LeaveType = leaveType,
            Year = year,
            AllocatedDays = allocatedDays
        };

    public void ApplyUsage(decimal days)
    {
        UsedDays = Math.Max(0, UsedDays + days);
        SetUpdated();
    }

    public void Adjust(decimal allocatedDays, string reason)
    {
        AllocatedDays = Math.Max(0, allocatedDays);
        AdjustmentReason = reason;
        SetUpdated();
    }
}
