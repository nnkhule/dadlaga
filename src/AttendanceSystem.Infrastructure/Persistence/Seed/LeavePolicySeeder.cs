using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AttendanceSystem.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds configurable leave policies and annual entitlements for local development.
/// </summary>
public static class LeavePolicySeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("LeavePolicySeeder");

        var policies = new (LeaveType Type, bool Paid, bool Deducts, decimal? Allowance, int Notice, bool Document, bool Reason, bool AdminOnly, bool AutoApprove)[]
        {
            (LeaveType.Annual, true, true, 15m, 3, false, true, false, false),
            (LeaveType.Sick, true, false, null, 0, true, true, false, false),
            (LeaveType.Unpaid, false, false, null, 0, false, true, false, false),
            (LeaveType.Birthday, true, false, null, 0, false, false, false, true),
            (LeaveType.Maternity, true, false, null, 0, true, true, true, false),
            (LeaveType.Other, false, false, null, 0, false, true, false, false)
        };

        foreach (var policy in policies)
        {
            if (!await context.LeavePolicies.AnyAsync(p => p.LeaveType == policy.Type))
            {
                context.LeavePolicies.Add(LeavePolicy.Create(
                    policy.Type, policy.Paid, policy.Deducts, policy.Allowance,
                    policy.Notice, policy.Document, policy.Reason,
                    policy.AdminOnly, policy.AutoApprove));
            }
        }

        await context.SaveChangesAsync();

        var year = DateTime.Today.Year;
        var employees = await context.Employees.AsNoTracking().Select(e => e.Id).ToListAsync();
        var existingBalances = await context.LeaveBalances
            .Where(b => b.Year == year && b.LeaveType == LeaveType.Annual)
            .Select(b => b.EmployeeId)
            .ToListAsync();
        var existingIds = existingBalances.ToHashSet();

        var approvedAnnualRows = await context.LeaveRequests
            .Where(l => l.LeaveType == LeaveType.Annual && l.Status == RequestStatus.Approved && l.StartDate.Year == year)
            .Select(l => new { l.EmployeeId, l.StartDate, l.EndDate })
            .ToListAsync();
        var approvedAnnual = approvedAnnualRows
            .GroupBy(l => l.EmployeeId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(l => (decimal)(l.EndDate.DayNumber - l.StartDate.DayNumber + 1)));

        foreach (var employeeId in employees.Where(id => !existingIds.Contains(id)))
        {
            var balance = LeaveBalance.Create(employeeId, LeaveType.Annual, year, 15m);
            if (approvedAnnual.TryGetValue(employeeId, out var usedDays))
                balance.ApplyUsage(usedDays);
            context.LeaveBalances.Add(balance);
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Leave policies and annual balances are ready for {EmployeeCount} employees.", employees.Count);
    }
}
