using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Infrastructure.Identity;
using AttendanceSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AttendanceSystem.Infrastructure.Persistence.Seed;

public static class EmployeeAccountSeeder
{
    private static readonly (Guid EmployeeId, string Email, string Name)[] TestEmployees =
    [
        (Guid.Parse("F5D4D08B-1E3A-4411-B6FC-022D49F90568"), "employee77@attendance.mn", "Employee 77"),
        (Guid.Parse("29DD9C14-5EAC-40CD-A957-039504412ED7"), "employee33@attendance.mn", "Employee 33"),
        (Guid.Parse("62B0239E-8346-43D1-ACC9-064E56B433E3"), "employee68@attendance.mn", "Employee 68"),
        (Guid.Parse("493912A9-8167-4A14-998F-06C82F294A65"), "employee31@attendance.mn", "Employee 31"),
        (Guid.Parse("6EB5250B-000B-4AAA-A31B-07AF3D890BF0"), "employee93@attendance.mn", "Employee 93"),
        (Guid.Parse("2E05330C-CE70-4183-8491-088CD4C443E4"), "employee81@attendance.mn", "Employee 81"),
        (Guid.Parse("6C42ABE7-53A9-4E3B-99DA-0A8598F734FB"), "employee24@attendance.mn", "Employee 24"),
        (Guid.Parse("2D32505A-B28D-4093-AD2B-0B541F3277D9"), "employee42@attendance.mn", "Employee 42"),
        (Guid.Parse("3676EF93-9964-4364-AA4D-0DD559CB6445"), "employee95@attendance.mn", "Employee 95"),
        (Guid.Parse("580A7C2B-B5B8-44EA-92AA-19B68AD2E85F"), "employee87@attendance.mn", "Employee 87"),
    ];

    public static async Task SeedEmployeeAccountsAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("EmployeeAccountSeeder");

        int created = 0;
        int skipped = 0;

        foreach (var (employeeId, email, name) in TestEmployees)
        {
            var existingUser = await userManager.FindByEmailAsync(email);
            if (existingUser is not null)
            {
                logger.LogInformation("User {Email} already exists", email);
                skipped++;
                continue;
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = name,
                EmployeeId = employeeId,
                EmailConfirmed = true
            };

            const string defaultPassword = "Employee@12345!";
            var result = await userManager.CreateAsync(user, defaultPassword);

            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(user, "Employee");
                logger.LogInformation("Created user {Email} with password Employee@12345!", email);
                created++;
            }
            else
            {
                logger.LogError("Failed to create user {Email}: {Errors}", email,
                    string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }

        logger.LogInformation("Employee account seeding completed. Created: {Created}, Skipped: {Skipped}", created, skipped);
    }
}
