namespace AttendanceSystem.Application.DTOs.AI;

public class EmployeeAiContextDto
{
    public string EmployeeName { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int LeaveBalance { get; set; }
    public bool IsCheckedInToday { get; set; }
    public DateTime? LastCheckIn { get; set; }
    public DateTime? LastCheckOut { get; set; }

    // ✅ Эдгээрийг нэм
    public int UsedLeaveDays { get; set; }
    public int TotalAnnualLeave { get; set; }
    public List<LeaveHistoryItem> RecentLeaves { get; set; } = new();
    public List<AttendanceHistoryItem> RecentAttendance { get; set; } = new();
    public int LateCountThisMonth { get; set; }
}

public class LeaveHistoryItem
{
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public int Days { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class AttendanceHistoryItem
{
    public string Date { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? CheckIn { get; set; }
    public string? CheckOut { get; set; }
}