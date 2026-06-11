using AttendanceSystem.Domain.Common;
using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Leave request including auto-generated birthday leave.
/// </summary>
public class LeaveRequest : BaseEntity
{
    public Guid EmployeeId { get; private set; }
    public LeaveType LeaveType { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public string? Reason { get; private set; }
    public RequestStatus Status { get; private set; } = RequestStatus.Pending;
    public bool IsBirthdayLeave { get; private set; }
    public Guid? ApprovedBy { get; private set; }
    public decimal TotalDays { get; private set; }

    public Employee? Employee { get; private set; }

    private LeaveRequest() { }

    public static LeaveRequest Create(Guid employeeId, LeaveType type, DateOnly start, DateOnly end,
        string? reason, bool isBirthdayLeave = false)
        => new()
        {
            EmployeeId = employeeId,
            LeaveType = type,
            StartDate = start,
            EndDate = end,
            Reason = reason,
            IsBirthdayLeave = isBirthdayLeave
        };

    public static LeaveRequest CreateApprovedBirthdayLeave(Guid employeeId, DateOnly leaveDate, Guid? systemUserId)
    {
        var request = Create(employeeId, LeaveType.Birthday, leaveDate, leaveDate,
            "Birthday leave (auto-approved)", true);
        request.Status = RequestStatus.Approved;
        request.ApprovedBy = systemUserId;
        return request;
    }

    public void Approve(Guid approverId) { Status = RequestStatus.Approved; ApprovedBy = approverId; SetUpdated(); }
    public void Reject(Guid approverId) { Status = RequestStatus.Rejected; ApprovedBy = approverId; SetUpdated(); }
}
