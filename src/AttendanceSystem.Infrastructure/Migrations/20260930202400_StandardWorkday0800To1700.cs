using AttendanceSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260930202400_StandardWorkday0800To1700")]
public sealed class StandardWorkday0800To1700 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE dbo.WorkSchedules
            SET ShiftStart = '08:00',
                ShiftEnd = '17:00',
                GraceMinutes = 0,
                BreakDurationMinutes = 60,
                StandardHoursPerDay = 8
            WHERE IsNightShift = 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE dbo.WorkSchedules
            SET ShiftStart = '09:00',
                ShiftEnd = '18:00',
                GraceMinutes = 10,
                StandardHoursPerDay = CASE WHEN Name = N'Standard 9-18' THEN 9 ELSE 8 END
            WHERE IsNightShift = 0;
            """);
    }
}
