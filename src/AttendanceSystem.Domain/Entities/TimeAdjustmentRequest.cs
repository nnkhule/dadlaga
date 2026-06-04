using AttendanceSystem.Domain.Common;
using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Employee request to correct attendance times with admin approval workflow.
/// </summary>
public class TimeAdjustmentRequest : BaseEntity
{
    public Guid EmployeeId { get; private set; }
    public Guid AttendanceRecordId { get; private set; }
    public DateTime? RequestedCheckIn { get; private set; }
    public DateTime? RequestedCheckOut { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public RequestStatus Status { get; private set; } = RequestStatus.Pending;
    public Guid? ReviewedBy { get; private set; }
    public string? ReviewNotes { get; private set; }

    public Employee? Employee { get; private set; }
    public AttendanceRecord? AttendanceRecord { get; private set; }

    private TimeAdjustmentRequest() { }

    public static TimeAdjustmentRequest Create(Guid employeeId, Guid recordId,
        DateTime? requestedCheckIn, DateTime? requestedCheckOut, string reason)
        => new()
        {
            EmployeeId = employeeId,
            AttendanceRecordId = recordId,
            RequestedCheckIn = requestedCheckIn,
            RequestedCheckOut = requestedCheckOut,
            Reason = reason
        };

    public void Approve(Guid reviewerId, string? notes)
    {
        Status = RequestStatus.Approved;
        ReviewedBy = reviewerId;
        ReviewNotes = notes;
        SetUpdated();
    }

    public void Reject(Guid reviewerId, string notes)
    {
        Status = RequestStatus.Rejected;
        ReviewedBy = reviewerId;
        ReviewNotes = notes;
        SetUpdated();
    }
}
