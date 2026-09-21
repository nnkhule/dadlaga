using AttendanceSystem.Domain.Common;
using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Organization-configurable rules for a leave type.
/// </summary>
public sealed class LeavePolicy : BaseEntity
{
    public LeaveType LeaveType { get; private set; }
    public bool IsPaid { get; private set; }
    public bool DeductsBalance { get; private set; }
    public decimal? AnnualAllowanceDays { get; private set; }
    public int AdvanceNoticeDays { get; private set; }
    public bool RequiresDocument { get; private set; }
    public bool RequiresReason { get; private set; }
    public bool AdminOnly { get; private set; }
    public bool AutoApprove { get; private set; }
    public bool IsActive { get; private set; } = true;

    private LeavePolicy() { }

    public static LeavePolicy Create(
        LeaveType leaveType,
        bool isPaid,
        bool deductsBalance,
        decimal? annualAllowanceDays,
        int advanceNoticeDays,
        bool requiresDocument,
        bool requiresReason,
        bool adminOnly,
        bool autoApprove = false)
        => new()
        {
            LeaveType = leaveType,
            IsPaid = isPaid,
            DeductsBalance = deductsBalance,
            AnnualAllowanceDays = annualAllowanceDays,
            AdvanceNoticeDays = advanceNoticeDays,
            RequiresDocument = requiresDocument,
            RequiresReason = requiresReason,
            AdminOnly = adminOnly,
            AutoApprove = autoApprove
        };

    public void Update(
        bool isPaid,
        bool deductsBalance,
        decimal? annualAllowanceDays,
        int advanceNoticeDays,
        bool requiresDocument,
        bool requiresReason,
        bool adminOnly,
        bool autoApprove,
        bool isActive)
    {
        IsPaid = isPaid;
        DeductsBalance = deductsBalance;
        AnnualAllowanceDays = annualAllowanceDays;
        AdvanceNoticeDays = advanceNoticeDays;
        RequiresDocument = requiresDocument;
        RequiresReason = requiresReason;
        AdminOnly = adminOnly;
        AutoApprove = autoApprove;
        IsActive = isActive;
        SetUpdated();
    }
}
