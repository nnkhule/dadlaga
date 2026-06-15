namespace AttendanceSystem.Application.DTOs.AI;

public class AdminAiContextDto
{
    public int TotalEmployees { get; set; }
    public int PresentToday { get; set; }
    public int AbsentToday { get; set; }
    public int OnLeaveToday { get; set; }
    public int PendingLeaveRequests { get; set; }
    public List<string> DepartmentNames { get; set; } = new();
}