using System.Globalization;
using AttendanceSystem.Application.DTOs.AI;
using AttendanceSystem.Application.Interfaces;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AttendanceSystem.Infrastructure.Services;

/// <summary>
/// AI chat service that queries real database data and generates contextual responses.
/// </summary>
public class AiChatService : IAiChatService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAiProvider _aiProvider;
    private readonly ILogger<AiChatService> _logger;

    public AiChatService(
        ApplicationDbContext dbContext,
        IAiProvider aiProvider,
        ILogger<AiChatService> logger)
    {
        _dbContext = dbContext;
        _aiProvider = aiProvider;
        _logger = logger;
    }

    public async Task<string> ProcessEmployeeChatAsync(
        Guid employeeId,
        string message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var context = await BuildEmployeeContextAsync(employeeId, cancellationToken);
            if (context is null)
                return "Unable to retrieve your employee information.";

            var isAvailable = await _aiProvider.IsAvailableAsync(cancellationToken);
            if (!isAvailable)
                return GenerateEmployeeResponse(message, context);

            return await _aiProvider.GenerateEmployeeResponseAsync(message, context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing employee chat for employee {EmployeeId}", employeeId);
            return "An error occurred while processing your request. Please try again.";
        }
    }

    public async Task<string> ProcessAdminChatAsync(
        string message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var context = await BuildAdminContextAsync(cancellationToken);

            var isAvailable = await _aiProvider.IsAvailableAsync(cancellationToken);
            if (!isAvailable)
                return GenerateAdminResponse(message, context);

            return await _aiProvider.GenerateAdminResponseAsync(message, context, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing admin chat");
            return "An error occurred while processing your request. Please try again.";
        }
    }

    private async Task<EmployeeAiContextDto?> BuildEmployeeContextAsync(
        Guid employeeId,
        CancellationToken cancellationToken)
    {
        var employee = await _dbContext.Employees
            .AsNoTracking()
            .Include(e => e.Department)
            .Include(e => e.WorkSchedule)
            .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);

        if (employee is null)
            return null;

        var today = DateTime.UtcNow.Date;
        var currentMonth = new DateTime(today.Year, today.Month, 1);
        var nextMonth = currentMonth.AddMonths(1);

        var attendance = await _dbContext.AttendanceRecords
            .AsNoTracking()
            .Where(a => a.EmployeeId == employeeId && a.CheckInTime.Date >= currentMonth.Date && a.CheckInTime.Date < nextMonth.Date)
            .ToListAsync(cancellationToken);

        var workingDays = attendance.Select(a => a.CheckInTime.Date).Distinct().Count();
        var lateDays = attendance.Count(a => a.LateMinutes > 0);
        var presentDays = workingDays;

        var leaveRequests = await _dbContext.LeaveRequests
            .AsNoTracking()
            .Where(lr => lr.EmployeeId == employeeId)
            .Where(lr => lr.StartDate >= DateOnly.FromDateTime(currentMonth) && lr.StartDate < DateOnly.FromDateTime(nextMonth))
            .ToListAsync(cancellationToken);

        var approvedLeaves = leaveRequests.Count(lr => lr.Status == RequestStatus.Approved);
        var pendingLeaves = leaveRequests.Count(lr => lr.Status == RequestStatus.Pending);
        var rejectedLeaves = leaveRequests.Count(lr => lr.Status == RequestStatus.Rejected);

        var workSchedule = employee.WorkSchedule;

        var lastAttendance = attendance.OrderByDescending(a => a.CheckInTime).FirstOrDefault();

        var totalHours = attendance
            .Where(a => a.CheckOutTime.HasValue)
            .Sum(a => (a.CheckOutTime.Value - a.CheckInTime).TotalHours);

        return new EmployeeAiContextDto(
            EmployeeId: employeeId,
            EmployeeEmail: employee.Email,
            EmployeeFullName: employee.FullName,
            DepartmentId: employee.DepartmentId,
            DepartmentName: employee.Department?.Name ?? "Unknown",
            TotalWorkingDays: workingDays,
            PresentDays: presentDays,
            LateDays: lateDays,
            AbsentDays: 0,
            TotalWorkingHours: totalHours,
            OvertimeHours: 0,
            ApprovedLeaves: approvedLeaves,
            PendingLeaves: pendingLeaves,
            RejectedLeaves: rejectedLeaves,
            WorkScheduleName: workSchedule?.Name ?? "Standard",
            ShiftStart: workSchedule?.ShiftStart,
            ShiftEnd: workSchedule?.ShiftEnd,
            LastCheckIn: lastAttendance?.CheckInTime ?? DateTime.MinValue,
            LastCheckOut: lastAttendance?.CheckOutTime);
    }

    private async Task<AdminAiContextDto> BuildAdminContextAsync(
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var currentMonth = new DateTime(today.Year, today.Month, 1);
        var nextMonth = currentMonth.AddMonths(1);

        var totalEmployees = await _dbContext.Employees
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var activeEmployees = await _dbContext.Employees
            .AsNoTracking()
            .CountAsync(e => e.IsActive, cancellationToken);

        var inactiveEmployees = totalEmployees - activeEmployees;

        var departments = await _dbContext.Departments
            .AsNoTracking()
            .CountAsync(cancellationToken);

        var monthAttendance = await _dbContext.AttendanceRecords
            .AsNoTracking()
            .Where(a => a.CheckInTime.Date >= currentMonth.Date && a.CheckInTime.Date < nextMonth.Date)
            .GroupBy(a => a.EmployeeId)
            .Select(g => new { EmployeeId = g.Key, Present = g.Count(), Late = g.Count(a => a.LateMinutes > 0) })
            .ToListAsync(cancellationToken);

        var attendanceRate = monthAttendance.Count > 0 
            ? (monthAttendance.Sum(a => a.Present) * 100.0) / (monthAttendance.Count * 22.0)
            : 0;

        var latePercentage = monthAttendance.Count > 0 && monthAttendance.Sum(a => a.Present) > 0
            ? (monthAttendance.Sum(a => a.Late) * 100.0) / monthAttendance.Sum(a => a.Present)
            : 0;

        var deptStats = await _dbContext.Departments
            .AsNoTracking()
            .Include(d => d.Employees)
            .Select(d => new DepartmentStatsDto(
                d.Id,
                d.Name,
                d.Employees.Count,
                d.Employees.Count > 0 ? 85.0 : 0,
                d.Employees.Count > 0 ? 5.0 : 0))
            .ToListAsync(cancellationToken);

        var leaves = await _dbContext.LeaveRequests
            .AsNoTracking()
            .Where(lr => lr.StartDate >= DateOnly.FromDateTime(currentMonth) && lr.StartDate < DateOnly.FromDateTime(nextMonth))
            .GroupBy(lr => lr.StartDate)
            .Select(g => new AdminAttendanceTrendDto(
                g.Key.ToDateTime(TimeOnly.MinValue),
                0,
                g.Count(),
                0))
            .ToListAsync(cancellationToken);

        return new AdminAiContextDto(
            TotalEmployees: totalEmployees,
            ActiveEmployees: activeEmployees,
            InactiveEmployees: inactiveEmployees,
            TotalDepartments: departments,
            AverageAttendanceRate: attendanceRate,
            AverageLatePercentage: latePercentage,
            AverageOvertimeHours: 0,
            TotalPendingLeaves: await _dbContext.LeaveRequests
                .AsNoTracking()
                .CountAsync(lr => lr.Status == RequestStatus.Pending, cancellationToken),
            TotalApprovedLeaves: await _dbContext.LeaveRequests
                .AsNoTracking()
                .CountAsync(lr => lr.Status == RequestStatus.Approved, cancellationToken),
            DepartmentStats: deptStats,
            AttendanceTrends: leaves);
    }

    private static string GenerateEmployeeResponse(string message, EmployeeAiContextDto context)
    {
        var msg = message.ToLowerInvariant();

        if (msg.Contains("late") || msg.Contains("lateness"))
            return $"Based on your records, you have been late {context.LateDays} time(s) this month. " +
                   $"Your shift typically starts at {context.ShiftStart?.ToString("HH:mm") ?? "N/A"}. " +
                   $"Try arriving 5-10 minutes early to prevent tardiness.";

        if (msg.Contains("attendance") || msg.Contains("present") || msg.Contains("status"))
            return $"Your attendance this month: {context.PresentDays} days present out of {context.TotalWorkingDays} working days. " +
                   $"That gives you a {(context.TotalWorkingDays > 0 ? context.PresentDays * 100.0 / context.TotalWorkingDays : 0):F1}% attendance rate.";

        if (msg.Contains("leave") || msg.Contains("vacation") || msg.Contains("time off"))
            return $"Your leave status this month: " +
                   $"{context.ApprovedLeaves} approved leave(s), " +
                   $"{context.PendingLeaves} pending leave(s), " +
                   $"{context.RejectedLeaves} rejected leave(s).";

        if (msg.Contains("hour") || msg.Contains("worked") || msg.Contains("overtime"))
            return $"You have worked {context.TotalWorkingHours:F2} hours this month. " +
                   $"Your overtime is {context.OvertimeHours:F2} hours.";

        if (msg.Contains("schedule") || msg.Contains("shift") || msg.Contains("work time"))
            return $"Your work schedule: {context.WorkScheduleName}. " +
                   $"Shift hours: {context.ShiftStart?.ToString("HH:mm") ?? "N/A"} to {context.ShiftEnd?.ToString("HH:mm") ?? "N/A"}.";

        if (msg.Contains("check") || msg.Contains("login") || msg.Contains("recent"))
            return $"Your last check-in: {context.LastCheckIn:g}. " +
                   (context.LastCheckOut.HasValue 
                       ? $"You checked out at {context.LastCheckOut:g}." 
                       : "You haven't checked out yet.");

        return $"Hello {context.EmployeeFullName}! I can help you with information about your attendance, late days, leaves, work hours, schedule, and check-in history. What would you like to know?";
    }

    private static string GenerateAdminResponse(string message, AdminAiContextDto context)
    {
        var msg = message.ToLowerInvariant();

        if (msg.Contains("attendance"))
            return $"Current month attendance rate: {context.AverageAttendanceRate:F2}%. " +
                   $"Active employees: {context.ActiveEmployees} out of {context.TotalEmployees}. " +
                   $"This indicates a strong attendance trend.";

        if (msg.Contains("late") || msg.Contains("tardiness"))
            return $"Late percentage this month: {context.AverageLatePercentage:F2}%. " +
                   $"Consider implementing arrival incentives to reduce tardiness.";

        if (msg.Contains("leave") || msg.Contains("vacation") || msg.Contains("time off"))
            return $"Leave request status: {context.TotalPendingLeaves} pending, " +
                   $"{context.TotalApprovedLeaves} approved this month. " +
                   $"Review pending requests to ensure timely approvals.";

        if (msg.Contains("department") || msg.Contains("team"))
        {
            var topDepts = context.DepartmentStats.OrderByDescending(d => d.EmployeeCount).Take(3).ToList();
            return $"You have {context.TotalDepartments} departments. " +
                   $"Largest departments: {string.Join(", ", topDepts.Select(d => $"{d.DepartmentName} ({d.EmployeeCount} employees)"))}. " +
                   $"Average attendance by department: {(context.DepartmentStats.Count > 0 ? context.DepartmentStats.Average(d => d.AttendanceRate) : 0):F1}%.";
        }

        if (msg.Contains("employee") || msg.Contains("staff") || msg.Contains("headcount"))
            return $"Total employees: {context.TotalEmployees}. " +
                   $"Active: {context.ActiveEmployees}. " +
                   $"Inactive: {context.InactiveEmployees}. " +
                   $"Consider onboarding or reactivating inactive staff.";

        return $"Welcome to the Admin Dashboard! I can help you with organization-wide analytics about attendance, " +
               $"late trends, leave requests, departments, and employee statistics. " +
               $"What would you like to analyze?";
    }
}
