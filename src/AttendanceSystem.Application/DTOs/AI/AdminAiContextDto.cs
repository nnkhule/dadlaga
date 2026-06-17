namespace AttendanceSystem.Application.DTOs.AI;

public class AdminAiContextDto
{
    public int TotalEmployees { get; set; }
    public int PresentToday { get; set; }
    public int AbsentToday { get; set; }
    public int OnLeaveToday { get; set; }
    public int LateTodayCount { get; set; }
    public int PendingLeaveRequests { get; set; }
    public List<string> DepartmentNames { get; set; } = new();

    // ✅ Шинээр нэмэгдсэн — сарын болон долоо хоногийн чиг хандлага
    public decimal AttendanceRateThisMonth { get; set; }
    public decimal AvgLateMinutesThisMonth { get; set; }
    public decimal TotalOvertimeHoursThisMonth { get; set; }
    public int SuspiciousRecordsThisWeek { get; set; }
    public List<DepartmentSnapshot> DepartmentBreakdown { get; set; } = new();
    public List<DailyTrendItem> Last7DaysTrend { get; set; } = new();
    public List<TopLateEmployeeItem> TopLateEmployeesThisMonth { get; set; } = new();
}

public class DepartmentSnapshot
{
    public string DepartmentName { get; set; } = string.Empty;
    public int EmployeeCount { get; set; }
    public int PresentToday { get; set; }
    public decimal AttendanceRate { get; set; }
}

public class DailyTrendItem
{
    public string Date { get; set; } = string.Empty;
    public int Present { get; set; }
    public int Absent { get; set; }
    public int Late { get; set; }
}

public class TopLateEmployeeItem
{
    public string EmployeeName { get; set; } = string.Empty;
    public int LateCount { get; set; }
    public decimal TotalLateMinutes { get; set; }
}
