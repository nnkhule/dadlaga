using AttendanceSystem.Domain.Common;
using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Alert raised when suspicious attendance patterns are detected.
/// </summary>
public class SuspiciousActivityAlert : BaseEntity
{
    public Guid? AttendanceRecordId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public SuspiciousAlertType AlertType { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public Guid? ReviewedBy { get; private set; }
    public ReviewStatus ReviewStatus { get; private set; } = ReviewStatus.Pending;
    public string? ReviewNotes { get; private set; }

    public AttendanceRecord? AttendanceRecord { get; private set; }
    public Employee? Employee { get; private set; }

    private SuspiciousActivityAlert() { }

    public static SuspiciousActivityAlert Create(Guid employeeId, SuspiciousAlertType type,
        string description, Guid? attendanceRecordId = null)
        => new()
        {
            EmployeeId = employeeId,
            AlertType = type,
            Description = description,
            AttendanceRecordId = attendanceRecordId
        };

    public void Review(Guid reviewerId, ReviewStatus status, string? notes)
    {
        ReviewedBy = reviewerId;
        ReviewStatus = status;
        ReviewNotes = notes;
        SetUpdated();
    }
}
