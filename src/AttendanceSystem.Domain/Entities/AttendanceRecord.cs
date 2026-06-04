using AttendanceSystem.Domain.Common;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Domain.Events;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Daily attendance record for an employee including check-in/out and computed metrics.
/// </summary>
public class AttendanceRecord : AggregateRoot
{
    public Guid EmployeeId { get; private set; }
    public DateOnly Date { get; private set; }
    public DateTime CheckInTime { get; private set; }
    public DateTime? CheckOutTime { get; private set; }
    public TimeSpan? BreakDuration { get; private set; }
    public AttendanceStatus Status { get; private set; }
    public decimal OvertimeHours { get; private set; }
    public decimal LateMinutes { get; private set; }
    public string? Notes { get; private set; }
    public bool IsManualEntry { get; private set; }
    public bool IsSuspicious { get; private set; }
    public bool IsAutoGeo { get; private set; }
    public VerificationMethod VerificationMethod { get; private set; }
    public string? CheckInPhotoUrl { get; private set; }
    public string? CheckOutPhotoUrl { get; private set; }
    public double? CheckInLatitude { get; private set; }
    public double? CheckInLongitude { get; private set; }
    public double? CheckOutLatitude { get; private set; }
    public double? CheckOutLongitude { get; private set; }
    public Guid? ApprovedBy { get; private set; }

    public Employee? Employee { get; private set; }

    private AttendanceRecord() { }

    /// <summary>
    /// Creates a new check-in record.
    /// </summary>
    public static AttendanceRecord CreateCheckIn(
        Guid employeeId,
        DateTime checkInTime,
        AttendanceStatus status,
        decimal lateMinutes,
        VerificationMethod method,
        bool isManual,
        bool isSuspicious,
        bool isAutoGeo,
        double? latitude,
        double? longitude,
        string? photoUrl,
        string? notes)
    {
        var record = new AttendanceRecord
        {
            EmployeeId = employeeId,
            Date = DateOnly.FromDateTime(checkInTime),
            CheckInTime = checkInTime,
            Status = status,
            LateMinutes = lateMinutes,
            VerificationMethod = method,
            IsManualEntry = isManual,
            IsSuspicious = isSuspicious,
            IsAutoGeo = isAutoGeo,
            CheckInLatitude = latitude,
            CheckInLongitude = longitude,
            CheckInPhotoUrl = photoUrl,
            Notes = notes
        };

        record.RaiseDomainEvent(new AttendanceCheckedInEvent(record.Id, employeeId, checkInTime));
        return record;
    }

    /// <summary>
    /// Completes check-out and updates status, break, and overtime.
    /// </summary>
    public void CheckOut(
        DateTime checkOutTime,
        AttendanceStatus status,
        TimeSpan breakDuration,
        decimal overtimeHours,
        VerificationMethod method,
        double? latitude,
        double? longitude,
        string? photoUrl)
    {
        CheckOutTime = checkOutTime;
        Status = status;
        BreakDuration = breakDuration;
        OvertimeHours = overtimeHours;
        if (method != VerificationMethod.Gps)
            VerificationMethod = method;
        CheckOutLatitude = latitude;
        CheckOutLongitude = longitude;
        CheckOutPhotoUrl = photoUrl;
        SetUpdated();
        RaiseDomainEvent(new AttendanceCheckedOutEvent(Id, EmployeeId, checkOutTime));
    }

    /// <summary>
    /// Updates metrics after admin-approved time adjustment.
    /// </summary>
    public void ApplyAdjustment(DateTime? checkIn, DateTime? checkOut, AttendanceStatus status,
        decimal lateMinutes, decimal overtimeHours, TimeSpan? breakDuration, Guid approvedBy)
    {
        if (checkIn.HasValue) CheckInTime = checkIn.Value;
        if (checkOut.HasValue) CheckOutTime = checkOut.Value;
        Status = status;
        LateMinutes = lateMinutes;
        OvertimeHours = overtimeHours;
        BreakDuration = breakDuration;
        ApprovedBy = approvedBy;
        IsManualEntry = true;
        SetUpdated();
    }

    public void MarkAbsent() { Status = AttendanceStatus.Absent; SetUpdated(); }
    public void MarkSuspicious() { IsSuspicious = true; SetUpdated(); }
    public void ClearSuspicious() { IsSuspicious = false; SetUpdated(); }
}
