using System;
using System.Collections.Generic;

namespace AttendanceSystem.API.Dtos
{
    public class EmployeeStatisticsDto
    {
        public SummaryDto Summary { get; set; }
        public AttendanceAnalyticsDto AttendanceAnalytics { get; set; }
        public List<AttendanceHistoryDto> AttendanceHistory { get; set; }
        public LeaveStatisticsDto LeaveStatistics { get; set; }
        public GpsStatisticsDto GpsStatistics { get; set; }
    }

    public class SummaryDto
    {
        public int TotalWorkingDays { get; set; }
        public int PresentDays { get; set; }
        public int LateDays { get; set; }
        public int AbsentDays { get; set; }
        public double TotalWorkingHours { get; set; }
        public double OvertimeHours { get; set; }
        public int LeaveRequests { get; set; }
        public int ApprovedLeaves { get; set; }
        // Percentage change properties (if backend provides)
        public double? TotalWorkingDaysChange { get; set; }
        public double? PresentDaysChange { get; set; }
        public double? LateDaysChange { get; set; }
        public double? AbsentDaysChange { get; set; }
        public double? TotalWorkingHoursChange { get; set; }
        public double? OvertimeHoursChange { get; set; }
        public double? LeaveRequestsChange { get; set; }
        public double? ApprovedLeavesChange { get; set; }
    }

    public class AttendanceAnalyticsDto
    {
        public double MonthlyAttendanceRate { get; set; }
        public double MonthlyWorkingHours { get; set; }
        public double MonthlyOvertime { get; set; }
        public double MonthlyLateMinutes { get; set; }
        public List<MonthlyAttendanceChartDto> MonthlyChartData { get; set; }
    }

    public class MonthlyAttendanceChartDto
    {
        public string Month { get; set; }
        public double AttendanceRate { get; set; }
        public double WorkingHours { get; set; }
        public double Overtime { get; set; }
        public double LateMinutes { get; set; }
    }

    public class AttendanceHistoryDto
    {
        public DateTime Date { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public double WorkHours { get; set; }
        public double OvertimeHours { get; set; }
        public int LateMinutes { get; set; }
        public string Status { get; set; }
        public string VerificationMethod { get; set; }
    }

    public class LeaveStatisticsDto
    {
        public int TotalRequests { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }
        public int Pending { get; set; }
        public List<LeaveHistoryDto> LeaveHistory { get; set; }
    }

    public class LeaveHistoryDto
    {
        public Guid LeaveRequestId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string LeaveType { get; set; }
        public string Status { get; set; }
        public DateTime RequestDate { get; set; }
    }

    public class GpsStatisticsDto
    {
        public int TotalGpsRecords { get; set; }
        public DateTime LastGpsActivity { get; set; }
        public int OfficeCheckins { get; set; }
        public int RemoteCheckins { get; set; }
        public List<GpsHistoryDto> GpsHistory { get; set; }
    }

    public class GpsHistoryDto
    {
        public Guid PingId { get; set; }
        public DateTime Timestamp { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public bool IsOfficeCheckin { get; set; }
        public string Location { get; set; }
    }
}
