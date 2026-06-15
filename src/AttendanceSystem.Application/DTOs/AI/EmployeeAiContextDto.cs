namespace AttendanceSystem.Application.DTOs.AI;

public class EmployeeAiContextDto
{
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int LeaveBalance { get; set; }
    public bool IsCheckedInToday { get; set; }
    public DateTime? LastCheckIn { get; set; }
    public DateTime? LastCheckOut { get; set; }
}