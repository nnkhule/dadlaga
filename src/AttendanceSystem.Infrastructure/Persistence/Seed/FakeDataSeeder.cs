using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AttendanceSystem.Infrastructure.Persistence.Seed;

/// <summary>
/// Adds a deterministic demo dataset for local development and reporting screens.
/// </summary>
public static class FakeDataSeeder
{
    private const string EmployeeCodePrefix = "FAKE-";
    private const int EmployeeCount = 120;

    /// <summary>
    /// Adds demo departments, employees, attendance records, and leave requests when absent.
    /// </summary>
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("FakeDataSeeder");

        if (await context.Employees.AnyAsync(e => e.EmployeeCode.StartsWith(EmployeeCodePrefix)))
        {
            logger.LogInformation("Fake data already exists; skipping demo seed.");
            return;
        }

        var random = new Random(20260920);
        var schedules = new[]
        {
            WorkSchedule.CreateStandard("Demo Standard 09:00-18:00"),
            WorkSchedule.CreateStandard("Demo Flexible 08:00-17:00"),
            WorkSchedule.CreateStandard("Demo Team 10:00-19:00")
        };
        var offices = new[]
        {
            OfficeLocation.Create("Demo Head Office", 47.9184, 106.9177, 150),
            OfficeLocation.Create("Demo West Branch", 47.9250, 106.8500, 150),
            OfficeLocation.Create("Demo East Branch", 47.9150, 107.0000, 150)
        };
        var departments = new[]
        {
            "Demo Human Resources", "Demo Engineering", "Demo Finance", "Demo Sales",
            "Demo Operations", "Demo Marketing", "Demo Customer Support", "Demo Legal"
        }.Select(name => Department.Create(name)).ToArray();

        context.WorkSchedules.AddRange(schedules);
        context.OfficeLocations.AddRange(offices);
        context.Departments.AddRange(departments);
        await context.SaveChangesAsync();

        var firstNames = new[] { "Бат", "Саруул", "Тэмүүлэн", "Номин", "Мөнх", "Анужин", "Эрдэнэ", "Хулан", "Ганзориг", "Амар" };
        var lastNames = new[] { "Бат", "Эрдэнэ", "Мөнх", "Сүх", "Отгон", "Төгөлдөр", "Дорж", "Болд", "Ган", "Цэцэг" };
        var employees = new List<Employee>(EmployeeCount);

        for (var index = 0; index < EmployeeCount; index++)
        {
            var department = departments[index % departments.Length];
            var fullName = $"{lastNames[index % lastNames.Length]} {firstNames[index % firstNames.Length]} {index + 1:000}";
            var email = $"fake.employee{index + 1:000}@attendance.local";
            employees.Add(Employee.Create(
                $"{EmployeeCodePrefix}{index + 1:000}",
                fullName,
                email,
                department.Id,
                schedules[index % schedules.Length].Id,
                offices[index % offices.Length].Id,
                DateOnly.FromDateTime(DateTime.Today.AddDays(-random.Next(180, 1800))),
                index % 10 == 0 ? ContractType.Intern : ContractType.FullTime,
                DateOnly.FromDateTime(DateTime.Today.AddYears(-25 - index % 15)),
                $"+976 99{index + 1:000000}"));
        }

        context.Employees.AddRange(employees);
        await context.SaveChangesAsync();

        var leaves = new List<LeaveRequest>();
        var attendance = new List<AttendanceRecord>();
        var today = DateOnly.FromDateTime(DateTime.Today);
        var from = today.AddDays(-365);

        foreach (var (employee, index) in employees.Select((employee, index) => (employee, index)))
        {
            var leaveStart = today.AddDays(-30 - index % 90);
            leaves.Add(LeaveRequest.Create(employee.Id, LeaveType.Annual, leaveStart, leaveStart.AddDays(2), "Demo annual leave"));
            leaves[^1].Approve(Guid.Empty);

            var pendingStart = today.AddDays(20 + index % 30);
            leaves.Add(LeaveRequest.Create(employee.Id, LeaveType.Sick, pendingStart, pendingStart.AddDays(1), "Demo pending request"));

            for (var date = from; date <= today; date = date.AddDays(1))
            {
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ||
                    date == leaveStart || date == leaveStart.AddDays(1) || date == leaveStart.AddDays(2))
                    continue;

                var roll = random.NextDouble();
                var checkIn = date.ToDateTime(new TimeOnly(8, 50)).AddMinutes(random.Next(0, 35));
                var status = roll < 0.07 ? AttendanceStatus.Late
                    : roll < 0.09 ? AttendanceStatus.HalfDay
                    : roll < 0.095 ? AttendanceStatus.EarlyLeave
                    : AttendanceStatus.Present;
                var lateMinutes = status == AttendanceStatus.Late ? random.Next(11, 46) : 0;
                var record = AttendanceRecord.CreateCheckIn(
                    employee.Id,
                    checkIn,
                    status,
                    lateMinutes,
                    (VerificationMethod)random.Next(0, 4),
                    isManual: random.NextDouble() < 0.08,
                    isSuspicious: random.NextDouble() < 0.01,
                    isAutoGeo: random.NextDouble() < 0.35,
                    latitude: 47.9184 + (random.NextDouble() - 0.5) / 100,
                    longitude: 106.9177 + (random.NextDouble() - 0.5) / 100,
                    photoUrl: null,
                    notes: status == AttendanceStatus.HalfDay ? "Demo half-day record" : null);

                var workedHours = status == AttendanceStatus.HalfDay ? 4m : 8m + (decimal)random.NextDouble() * 2m;
                var checkOut = checkIn.AddHours((double)workedHours + 1);
                record.CheckOut(
                    checkOut,
                    status,
                    TimeSpan.FromHours(1),
                    Math.Max(0, workedHours - 8),
                    Math.Max(0, 8 - workedHours),
                    VerificationMethod.Gps,
                    47.9184 + (random.NextDouble() - 0.5) / 100,
                    106.9177 + (random.NextDouble() - 0.5) / 100,
                    null);
                attendance.Add(record);
            }
        }

        context.LeaveRequests.AddRange(leaves);
        context.AttendanceRecords.AddRange(attendance);
        await context.SaveChangesAsync();

        logger.LogInformation(
            "Fake data seeded: {Employees} employees, {Attendance} attendance records, {Leaves} leave requests.",
            employees.Count, attendance.Count, leaves.Count);
    }
}