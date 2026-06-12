using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AttendanceSystem.Application.DTOs.Attendance;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using AttendanceSystem.API.Dtos;

namespace AttendanceSystem.API.Services
{
    /// <summary>
    /// Provides employee statistics and analytics data for the API.
    /// </summary>
    public class EmployeeStatisticsService : IEmployeeStatisticsService
    {
        private readonly ApplicationDbContext _db;

        /// <summary>
        /// Creates a new instance of <see cref="EmployeeStatisticsService"/>.
        /// </summary>
        public EmployeeStatisticsService(ApplicationDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Gets aggregated employee statistics for the current user.
        /// </summary>
        public async Task<EmployeeStatisticsDto> GetEmployeeStatisticsAsync(Guid employeeId)
        {
            // Define the period as the current month (from first day to today)
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var startOfMonth = new DateOnly(today.Year, today.Month, 1);
            var endOfMonth = today; // We want up to today

            // Get the employee with work schedule and office location
            var employee = await _db.Employees
                .Include(e => e.WorkSchedule)
                .Include(e => e.OfficeLocation)
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            if (employee == null)
            {
                throw new KeyNotFoundException($"Employee with id {employeeId} not found.");
            }

            // Get holidays for the period (we'll get all holidays and filter by period)
            var holidays = await _db.Holidays
                .Where(h => h.Date >= startOfMonth && h.Date <= endOfMonth)
                .Select(h => h.Date)
                .ToListAsync();

            // Get attendance records for the period
            var attendanceRecords = await _db.AttendanceRecords
                .Where(a => a.EmployeeId == employeeId && a.Date >= startOfMonth && a.Date <= endOfMonth)
                .ToListAsync();

            // Get leave requests for the period (for summary)
            // Project only the fields we need to avoid selecting unmigrated columns like TotalDays
            var leaveRequests = await _db.LeaveRequests
                .Where(l => l.EmployeeId == employeeId && l.StartDate >= startOfMonth && l.EndDate <= endOfMonth)
                .Select(l => new {
                    l.Id,
                    l.EmployeeId,
                    l.StartDate,
                    l.EndDate,
                    l.Status,
                    l.CreatedAt,
                    l.LeaveType
                })
                .ToListAsync();

            // Get GPS pings for the employee (we'll get all for now, but we can filter by period if needed)
            var gpsPings = await _db.GpsPings
                .Where(g => g.EmployeeId == employeeId)
                .ToListAsync();

            // Calculate total working days in the period (based on work schedule and holidays)
            int totalWorkingDays = 0;
            for (var date = startOfMonth; date <= endOfMonth; date = date.AddDays(1))
            {
                var dayOfWeek = date.DayOfWeek;
                if (employee.WorkSchedule?.IsWorkDay(dayOfWeek) == true && !holidays.Contains(date))
                {
                    totalWorkingDays++;
                }
            }

            // Calculate present days, late days, absent days
            int presentDays = attendanceRecords.Count(a => a.Status != AttendanceStatus.Absent && a.Status != AttendanceStatus.OnLeave);
            int lateDays = attendanceRecords.Count(a => a.Status == AttendanceStatus.Late || a.LateMinutes > 0);
            int absentDays = attendanceRecords.Count(a => a.Status == AttendanceStatus.Absent);

            // Calculate total working hours and overtime hours
            double totalWorkingHours = 0.0;
            double overtimeHours = 0.0;

            foreach (var record in attendanceRecords)
            {
                if (record.CheckOutTime.HasValue)
                {
                    var workDuration = record.CheckOutTime.Value - record.CheckInTime;
                    var breakDuration = record.BreakDuration ?? TimeSpan.Zero;
                    var netWorkHours = workDuration.TotalHours - breakDuration.TotalHours;
                    totalWorkingHours += Math.Max(0, netWorkHours); // Ensure non-negative
                }

                overtimeHours += (double)record.OvertimeHours;
            }

            // Leave requests counts
            int leaveRequestsCount = leaveRequests.Count;
            int approvedLeaves = leaveRequests.Count(l => l.Status == RequestStatus.Approved);

            // Build summary
            var summary = new SummaryDto
            {
                TotalWorkingDays = totalWorkingDays,
                PresentDays = presentDays,
                LateDays = lateDays,
                AbsentDays = absentDays,
                TotalWorkingHours = totalWorkingHours,
                OvertimeHours = overtimeHours,
                LeaveRequests = leaveRequestsCount,
                ApprovedLeaves = approvedLeaves
                // Percentage change not implemented (would require comparing to previous period)
            };

            // Attendance analytics (for the current month)
            // We'll compute monthly attendance rate, monthly working hours, monthly overtime, monthly late minutes
            // We already have the data for the period (current month to today)
            double monthlyAttendanceRate = 0.0;
            double monthlyWorkingHours = totalWorkingHours; // This is the net working hours for the period
            double monthlyOvertime = overtimeHours;
            double monthlyLateMinutes = (double)attendanceRecords.Sum(a => a.LateMinutes);

            // Attendance rate: (present days) / (total working days) * 100
            if (totalWorkingDays > 0)
            {
                monthlyAttendanceRate = (double)presentDays / totalWorkingDays * 100.0;
            }

            // For the chart, we might want to show data for the last few months? 
            // The requirement says: "Monthly Attendance Rate", "Monthly Working Hours", etc.
            // And then "Use real chart data returned by backend."
            // We'll return the data for the current month only for the chart? Or we can return the last 6 months?
            // Let's return the last 6 months of data for the chart.

            var monthlyChartData = await GetMonthlyChartDataAsync(employeeId, 6);

            var attendanceAnalytics = new AttendanceAnalyticsDto
            {
                MonthlyAttendanceRate = monthlyAttendanceRate,
                MonthlyWorkingHours = monthlyWorkingHours,
                MonthlyOvertime = monthlyOvertime,
                MonthlyLateMinutes = monthlyLateMinutes,
                MonthlyChartData = monthlyChartData
            };

            // Attendance history (we'll get for the same period? Or we can get for the last 30 days? 
            // The requirement doesn't specify. Let's get for the same period as the summary (current month) for now.
            var attendanceHistory = await GetAttendanceHistoryAsync(employeeId, startOfMonth, endOfMonth);

            // Leave statistics
            var leaveStatistics = await GetLeaveStatisticsAsync(employeeId);

            // GPS statistics
            var gpsStatistics = await GetGpsStatisticsAsync(employeeId);

            return new EmployeeStatisticsDto
            {
                Summary = summary,
                AttendanceAnalytics = attendanceAnalytics,
                AttendanceHistory = attendanceHistory,
                LeaveStatistics = leaveStatistics,
                GpsStatistics = gpsStatistics
            };
        }

        /// <summary>
        /// Gets attendance history records for the given period.
        /// </summary>
        public async Task<List<AttendanceHistoryDto>> GetAttendanceHistoryAsync(Guid employeeId, DateOnly from, DateOnly to)
        {
            var records = await _db.AttendanceRecords
                .Where(a => a.EmployeeId == employeeId && a.Date >= from && a.Date <= to)
                .OrderByDescending(a => a.Date)
                .Select(a => new AttendanceHistoryDto
                {
                    Date = a.Date.ToDateTime(TimeOnly.MinValue),
                    CheckInTime = a.CheckInTime,
                    CheckOutTime = a.CheckOutTime,
                    WorkHours = a.CheckOutTime.HasValue ? 
                        (a.CheckOutTime.Value - a.CheckInTime).TotalHours : 0,
                    OvertimeHours = (double)a.OvertimeHours,
                    LateMinutes = (int)a.LateMinutes,
                    Status = a.Status.ToString(),
                    VerificationMethod = a.VerificationMethod.ToString()
                })
                .ToListAsync();

            return records;
        }

        /// <summary>
        /// Gets leave statistics for the given employee.
        /// </summary>
        public async Task<LeaveStatisticsDto> GetLeaveStatisticsAsync(Guid employeeId)
        {
            // Project only needed fields to avoid selecting unmigrated columns
            var leaveRequests = await _db.LeaveRequests
                .Where(l => l.EmployeeId == employeeId)
                .Select(l => new {
                    l.Id,
                    l.StartDate,
                    l.EndDate,
                    l.LeaveType,
                    l.Status,
                    l.CreatedAt
                })
                .ToListAsync();

            var totalRequests = leaveRequests.Count;
            var approved = leaveRequests.Count(l => l.Status == RequestStatus.Approved);
            var rejected = leaveRequests.Count(l => l.Status == RequestStatus.Rejected);
            var pending = leaveRequests.Count(l => l.Status == RequestStatus.Pending);

            // We'll also get the leave history for the table in the UI (maybe the last 10?)
            var leaveHistory = leaveRequests
                .OrderByDescending(l => l.CreatedAt)
                .Take(10)
                .Select(l => new LeaveHistoryDto
                {
                    LeaveRequestId = l.Id,
                    StartDate = l.StartDate.ToDateTime(TimeOnly.MinValue),
                    EndDate = l.EndDate.ToDateTime(TimeOnly.MinValue),
                    LeaveType = l.LeaveType.ToString(),
                    Status = l.Status.ToString(),
                    RequestDate = l.CreatedAt
                })
                .ToList();

            return new LeaveStatisticsDto
            {
                TotalRequests = totalRequests,
                Approved = approved,
                Rejected = rejected,
                Pending = pending,
                LeaveHistory = leaveHistory
            };
        }

        /// <summary>
        /// Gets GPS statistics and recent ping history for the given employee.
        /// </summary>
        public async Task<GpsStatisticsDto> GetGpsStatisticsAsync(Guid employeeId)
        {
            var gpsPings = await _db.GpsPings
                .Where(g => g.EmployeeId == employeeId)
                .OrderByDescending(g => g.RecordedAt)
                .ToListAsync();

            int totalGpsRecords = gpsPings.Count;
            DateTime lastGpsActivity = gpsPings.Any() ? gpsPings.First().RecordedAt : DateTime.MinValue;

            // We need to determine office check-ins vs remote check-ins.
            // We need the employee's office location and the office's radius.
            // We already have the employee from the main method, but we are in a different method.
            // We'll get the employee again (or we can pass it, but for simplicity we'll get it again).
            var employee = await _db.Employees
                .Include(e => e.OfficeLocation)
                .FirstOrDefaultAsync(e => e.Id == employeeId);

            int officeCheckins = 0;
            if (employee?.OfficeLocation != null)
            {
                var office = employee.OfficeLocation;
                foreach (var ping in gpsPings)
                {
                    var distance = GetDistanceMeters(
                        ping.Latitude, ping.Longitude,
                        office.Latitude, office.Longitude);
                    if (distance <= office.RadiusMeters)
                    {
                        officeCheckins++;
                    }
                }
            }

            int remoteCheckins = totalGpsRecords - officeCheckins;

            // GPS history: we'll return the last 50 pings for the history table
            var gpsHistoryList = new List<GpsHistoryDto>();
            if (gpsPings.Any())
            {
                var historyPings = gpsPings.Take(50).ToList();
                foreach (var ping in historyPings)
                {
                    bool isOffice = false;
                    if (employee?.OfficeLocation != null)
                    {
                        var distance = GetDistanceMeters(
                            ping.Latitude, ping.Longitude,
                            employee.OfficeLocation.Latitude, employee.OfficeLocation.Longitude);
                        isOffice = distance <= employee.OfficeLocation.RadiusMeters;
                    }

                    gpsHistoryList.Add(new GpsHistoryDto
                    {
                        PingId = ping.Id,
                        Timestamp = ping.RecordedAt,
                        Latitude = ping.Latitude,
                        Longitude = ping.Longitude,
                        IsOfficeCheckin = isOffice,
                        Location = $"Lat: {ping.Latitude}, Lng: {ping.Longitude}"
                    });
                }
            }

            return new GpsStatisticsDto
            {
                TotalGpsRecords = totalGpsRecords,
                LastGpsActivity = lastGpsActivity,
                OfficeCheckins = officeCheckins,
                RemoteCheckins = remoteCheckins,
                GpsHistory = gpsHistoryList
            };
        }

        private async Task<List<MonthlyAttendanceChartDto>> GetMonthlyChartDataAsync(Guid employeeId, int months)
        {
            var chartData = new List<MonthlyAttendanceChartDto>();
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            for (int i = 0; i < months; i++)
            {
                // Calculate the month for i months ago
                var month = today.AddMonths(-i);
                var startOfMonth = new DateOnly(month.Year, month.Month, 1);
                var endOfMonth = startOfMonth.AddMonths(1).AddDays(-1); // last day of the month

                // Get holidays for this month
                var holidays = await _db.Holidays
                    .Where(h => h.Date >= startOfMonth && h.Date <= endOfMonth)
                    .Select(h => h.Date)
                    .ToListAsync();

                // Get attendance records for the employee for this month
                var attendanceRecords = await _db.AttendanceRecords
                    .Where(a => a.EmployeeId == employeeId && a.Date >= startOfMonth && a.Date <= endOfMonth)
                    .ToListAsync();

                // Get the employee's work schedule
                var employee = await _db.Employees
                    .Include(e => e.WorkSchedule)
                    .FirstOrDefaultAsync(e => e.Id == employeeId);

                if (employee == null)
                {
                    chartData.Add(new MonthlyAttendanceChartDto
                    {
                        Month = month.ToString("yyyy-MM"),
                        AttendanceRate = 0,
                        WorkingHours = 0,
                        Overtime = 0,
                        LateMinutes = 0
                    });
                    continue;
                }

                // Calculate total working days in the month (based on work schedule and holidays)
                int totalWorkingDays = 0;
                for (var date = startOfMonth; date <= endOfMonth; date = date.AddDays(1))
                {
                    var dayOfWeek = date.DayOfWeek;
                    if (employee.WorkSchedule?.IsWorkDay(dayOfWeek) == true && !holidays.Contains(date))
                    {
                        totalWorkingDays++;
                    }
                }

                // Calculate present days, late days, overtime hours, late minutes for the month
                int presentDays = attendanceRecords.Count(a => a.Status != AttendanceStatus.Absent && a.Status != AttendanceStatus.OnLeave);
                int lateDays = attendanceRecords.Count(a => a.Status == AttendanceStatus.Late || a.LateMinutes > 0);
                double overtimeHours = (double)attendanceRecords.Sum(a => a.OvertimeHours);
                double lateMinutes = (double)attendanceRecords.Sum(a => a.LateMinutes);

                // Calculate attendance rate
                double attendanceRate = 0.0;
                if (totalWorkingDays > 0)
                {
                    attendanceRate = (double)presentDays / totalWorkingDays * 100.0;
                }

                // Calculate total working hours (net of breaks) for the month
                double workingHours = 0.0;
                foreach (var record in attendanceRecords)
                {
                    if (record.CheckOutTime.HasValue)
                    {
                        var workDuration = record.CheckOutTime.Value - record.CheckInTime;
                        var breakDuration = record.BreakDuration ?? TimeSpan.Zero;
                        var netWorkHours = workDuration.TotalHours - breakDuration.TotalHours;
                        workingHours += Math.Max(0, netWorkHours);
                    }
                }

                chartData.Add(new MonthlyAttendanceChartDto
                {
                    Month = month.ToString("yyyy-MM"),
                    AttendanceRate = Math.Round(attendanceRate, 2),
                    WorkingHours = Math.Round(workingHours, 2),
                    Overtime = Math.Round(overtimeHours, 2),
                    LateMinutes = Math.Round(lateMinutes, 2)
                });
            }

            // We collected data from the current month going back, but the chart should be in chronological order (oldest first)
            chartData.Reverse();
            return chartData;
        }

        private double GetDistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double radius = 6371000; // Earth radius in meters
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return radius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private double ToRadians(double degrees) => degrees * Math.PI / 180;
    }
}
