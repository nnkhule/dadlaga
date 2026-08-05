using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceSystem.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for WorkShiftPlan entity.
/// </summary>
public class WorkShiftPlanConfiguration : IEntityTypeConfiguration<WorkShiftPlan>
{
    public void Configure(EntityTypeBuilder<WorkShiftPlan> builder)
    {
        builder.ToTable("WorkShiftPlans");

        builder.HasKey(wsp => wsp.Id);

        builder.Property(wsp => wsp.EmployeeId)
            .IsRequired();

        builder.Property(wsp => wsp.WorkScheduleId)
            .IsRequired();

        builder.Property(wsp => wsp.PlanDate)
            .IsRequired();

        builder.Property(wsp => wsp.Status)
            .IsRequired()
            .HasConversion(
                v => v.ToString(),
                v => (ShiftPlanStatus)Enum.Parse(typeof(ShiftPlanStatus), v));

        builder.Property(wsp => wsp.Notes)
            .HasMaxLength(500);

        // Relationships
        builder.HasOne(wsp => wsp.Employee)
            .WithMany()
            .HasForeignKey(wsp => wsp.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(wsp => wsp.WorkSchedule)
            .WithMany()
            .HasForeignKey(wsp => wsp.WorkScheduleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
