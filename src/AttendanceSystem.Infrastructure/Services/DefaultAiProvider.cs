using AttendanceSystem.Application.DTOs.AI;
using AttendanceSystem.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AttendanceSystem.Infrastructure.Services;

/// <summary>
/// Default AI provider that generates responses based on database context.
/// No external AI service required. Can be replaced with OpenAI, Ollama, or NVIDIA NIM.
/// </summary>
public class DefaultAiProvider : IAiProvider
{
    private readonly ILogger<DefaultAiProvider> _logger;

    public DefaultAiProvider(ILogger<DefaultAiProvider> logger)
    {
        _logger = logger;
    }

    public Task<string> GenerateEmployeeResponseAsync(
        string message,
        EmployeeAiContextDto context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating employee AI response for employee {EmployeeId}", context.EmployeeId);
        return Task.FromResult(GenerateResponse(message, context));
    }

    public Task<string> GenerateAdminResponseAsync(
        string message,
        AdminAiContextDto context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating admin AI response");
        return Task.FromResult(GenerateResponse(message, context));
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(true);
    }

    private static string GenerateResponse(string message, EmployeeAiContextDto context)
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

    private static string GenerateResponse(string message, AdminAiContextDto context)
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
                   $"Average attendance by department: {context.DepartmentStats.Average(d => d.AttendanceRate):F1}%.";
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
