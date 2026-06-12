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
        // Do not map `TotalDays` to the DB when the column may be missing in
        // deployed databases. The migration `FixSchemaIssues` adds this
        // computed column; if you apply that migration you can restore the
        // computed mapping below.
        builder.Ignore(x => x.TotalDays);
        builder.HasIndex(x => new { x.EmployeeId, x.Status })
            .HasDatabaseName("IX_Leave_EmployeeId_Status")
            .IncludeProperties(x => new { x.StartDate, x.EndDate, x.LeaveType });
    }
}
