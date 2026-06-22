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

    /// <summary>"Daily" (бүтэн өдрөөр) эсвэл "Hourly" (цагаар) чөлөө мөн эсэх.</summary>
    public string LeaveMode { get; private set; } = "Daily";
    public TimeOnly? StartTime { get; private set; }
    public TimeOnly? EndTime { get; private set; }
    /// <summary>Зөвхөн цагийн чөлөөнд хэрэглэгдэх — нийт хичнээн цаг.</summary>
    public decimal? Hours { get; private set; }

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
            IsBirthdayLeave = isBirthdayLeave,
            LeaveMode = "Daily"
        };

    public static LeaveRequest CreateHourly(Guid employeeId, LeaveType type, DateOnly date,
        TimeOnly startTime, TimeOnly endTime, decimal hours, string? reason)
        => new()
        {
            EmployeeId = employeeId,
            LeaveType = type,
            StartDate = date,
            EndDate = date,
            Reason = reason,
            LeaveMode = "Hourly",
            StartTime = startTime,
            EndTime = endTime,
            Hours = hours
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