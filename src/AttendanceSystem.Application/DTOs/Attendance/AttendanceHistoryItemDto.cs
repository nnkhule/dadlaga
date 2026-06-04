namespace AttendanceSystem.Application.DTOs.Attendance;

/// <summary>
/// Simple attendance history item for display in mobile UI.
/// </summary>
public record AttendanceHistoryItemDto(
    DateOnly Date,
    DateTime? CheckInTime,
    DateTime? CheckOutTime);
