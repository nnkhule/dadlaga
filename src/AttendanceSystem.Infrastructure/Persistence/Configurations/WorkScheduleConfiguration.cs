using AttendanceSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceSystem.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for <see cref="WorkSchedule"/>.
/// </summary>
public class WorkScheduleConfiguration : IEntityTypeConfiguration<WorkSchedule>
{
    public void Configure(EntityTypeBuilder<WorkSchedule> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint("CHK_WorkDays_Range", "[WorkDays] BETWEEN 0 AND 127"));
    }
}
