using AttendanceSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceSystem.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for <see cref="AttendanceRecord"/>.
/// </summary>
public class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("AttendanceRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OvertimeHours).HasPrecision(10, 2);
        builder.Property(x => x.LateMinutes).HasPrecision(10, 2);
        builder.HasIndex(x => new { x.EmployeeId, x.Date })
            .IsUnique()
            .HasDatabaseName("UQ_Attendance_Employee_Date");
        builder.HasIndex(x => new { x.EmployeeId, x.Date })
            .HasDatabaseName("IX_Attendance_EmployeeId_Date")
            .IncludeProperties(x => new { x.Status, x.CheckInTime, x.CheckOutTime });
        builder.HasOne(x => x.Employee).WithMany(e => e.AttendanceRecords).HasForeignKey(x => x.EmployeeId);
    }
}
