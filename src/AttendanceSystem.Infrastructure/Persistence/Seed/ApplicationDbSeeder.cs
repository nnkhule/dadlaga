using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AttendanceSystem.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds roles, admin user, departments, and sample employee.
/// </summary>
public static class ApplicationDbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("ApplicationDbSeeder");
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        await context.Database.MigrateAsync();

        foreach (var (name, desc) in RoleDefinitions.All)
        {
            if (!await roleManager.RoleExistsAsync(name))
                await roleManager.CreateAsync(new ApplicationRole { Name = name, Description = desc });
        }

        if (!await context.Departments.AnyAsync())
        {
            var schedule = WorkSchedule.CreateStandard();
            var office = OfficeLocation.Create("Head Office Ulaanbaatar", 47.9123, 106.9308, 100);
            var dept = Department.Create("Human Resources");
            context.WorkSchedules.Add(schedule);
            context.OfficeLocations.Add(office);
            context.Departments.Add(dept);
            await context.SaveChangesAsync();

            var employee = Employee.Create(
                "EMP001",
                "Б.Бат",
                "bat@attendance.local",
                dept.Id,
                schedule.Id,
                office.Id,
                DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1)),
                ContractType.FullTime,
                new DateOnly(1990, 5, 15));

            context.Employees.Add(employee);
            await context.SaveChangesAsync();

            const string adminEmail = "admin@attendance.local";
            if (await userManager.FindByEmailAsync(adminEmail) is null)
            {
                var admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FullName = "System Administrator",
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(admin, "Admin@12345!");
                await userManager.AddToRoleAsync(admin, "SuperAdmin");
            }

            const string empEmail = "bat@attendance.local";
            if (await userManager.FindByEmailAsync(empEmail) is null)
            {
                var empUser = new ApplicationUser
                {
                    UserName = empEmail,
                    Email = empEmail,
                    FullName = employee.FullName,
                    EmployeeId = employee.Id,
                    EmailConfirmed = true
                };
                await userManager.CreateAsync(empUser, "Employee@12345!");
                await userManager.AddToRoleAsync(empUser, "Employee");
                employee.LinkUser(empUser.Id);
                await context.SaveChangesAsync();
            }

            SeedMongolianHolidays(context);
            await context.SaveChangesAsync();
        }

        logger.LogInformation("Database seed completed.");
    }

    private static void SeedMongolianHolidays(ApplicationDbContext context)
    {
        if (context.Holidays.Any()) return;
        var year = DateTime.UtcNow.Year;
        var holidays = new[]
        {
            ("New Year", new DateOnly(year, 1, 1)),
            ("Tsagaan Sar", new DateOnly(year, 2, 10)),
            ("International Women's Day", new DateOnly(year, 3, 8)),
            ("Children's Day", new DateOnly(year, 6, 1)),
            ("Naadam", new DateOnly(year, 7, 11)),
            ("Republic Day", new DateOnly(year, 11, 26)),
            ("Independence Day", new DateOnly(year, 12, 29))
        };
        foreach (var (name, date) in holidays)
            context.Holidays.Add(Holiday.Create(name, date, recurring: true));
    }
}

internal static class RoleDefinitions
{
    public static readonly (string Name, string Description)[] All =
    [
        ("SuperAdmin", "Full system access"),
        ("HRManager", "HR and reports management"),
        ("DepartmentHead", "Department team management"),
        ("Employee", "Self-service attendance"),
        ("Auditor", "Read-only audit access")
    ];
}
