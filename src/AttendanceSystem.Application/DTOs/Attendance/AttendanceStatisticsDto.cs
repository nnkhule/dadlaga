namespace AttendanceSystem.Application.DTOs.Attendance;

/// <summary>
/// Attendance statistics for the current calendar month.
/// </summary>
public record AttendanceStatisticsDto(
    int PresentDays,
    int WorkingDays,
    int LateCount,
    decimal OvertimeHours,
    string MonthLabel,
    IEnumerable<AttendanceHistoryItemDto> RecentRecords);
