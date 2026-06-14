namespace AttendanceSystem.Application.DTOs.AI;

/// <summary>
/// Context data for admin AI chat queries.
/// </summary>
public record AdminAiContextDto(
    int TotalEmployees,
    int ActiveEmployees,
    int InactiveEmployees,
    int TotalDepartments,
    double AverageAttendanceRate,
    double AverageLatePercentage,
    double AverageOvertimeHours,
    int TotalPendingLeaves,
    int TotalApprovedLeaves,
    List<DepartmentStatsDto> DepartmentStats,
    List<AdminAttendanceTrendDto> AttendanceTrends);

public record DepartmentStatsDto(
    Guid DepartmentId,
    string DepartmentName,
    int EmployeeCount,
    double AttendanceRate,
    double LatePercentage);

public record AdminAttendanceTrendDto(
    DateTime Date,
    int PresentCount,
    int AbsentCount,
    int LateCount);
