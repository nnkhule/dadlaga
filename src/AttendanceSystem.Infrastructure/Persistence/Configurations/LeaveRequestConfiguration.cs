using AttendanceSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceSystem.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for <see cref="LeaveRequest"/>.
/// </summary>
public class LeaveRequestConfiguration : IEntityTypeConfiguration<LeaveRequest>
{
    public void Configure(EntityTypeBuilder<LeaveRequest> builder)
    {
        builder.ToTable("LeaveRequests");
        builder.Property(x => x.TotalDays)
            .HasPrecision(5, 1)
            .HasComputedColumnSql("CAST(DATEDIFF(day, [StartDate], [EndDate]) + 1 AS decimal(5,1))", stored: true);
        builder.HasIndex(x => new { x.EmployeeId, x.Status })
            .HasDatabaseName("IX_Leave_EmployeeId_Status")
            .IncludeProperties(x => new { x.StartDate, x.EndDate, x.LeaveType });
    }
}
