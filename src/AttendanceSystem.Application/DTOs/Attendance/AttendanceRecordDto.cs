using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Application.DTOs.Attendance;

/// <summary>
/// Attendance record data transfer object.
/// </summary>
public record AttendanceRecordDto(
    Guid Id,
    Guid EmployeeId,
    DateOnly Date,
    DateTime CheckInTime,
    DateTime? CheckOutTime,
    AttendanceStatus Status,
    decimal OvertimeHours,
    decimal LateMinutes,
    VerificationMethod VerificationMethod,
    bool IsSuspicious,
    bool IsAutoGeo);
