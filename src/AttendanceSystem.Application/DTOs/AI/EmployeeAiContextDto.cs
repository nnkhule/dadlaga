namespace AttendanceSystem.Application.DTOs.AI;

/// <summary>
/// Context data for employee AI chat queries.
/// </summary>
public record EmployeeAiContextDto(
    Guid EmployeeId,
    string EmployeeEmail,
    string EmployeeFullName,
    Guid DepartmentId,
    string DepartmentName,
    int TotalWorkingDays,
    int PresentDays,
    int LateDays,
    int AbsentDays,
    double TotalWorkingHours,
    double OvertimeHours,
    int ApprovedLeaves,
    int PendingLeaves,
    int RejectedLeaves,
    string WorkScheduleName,
    TimeOnly? ShiftStart,
    TimeOnly? ShiftEnd,
    DateTime LastCheckIn,
    DateTime? LastCheckOut);
