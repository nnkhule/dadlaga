using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceSystem.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for TaskItem entity.
/// </summary>
public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("TaskItems");

        builder.HasKey(ti => ti.Id);

        builder.Property(ti => ti.WorkShiftPlanId)
            .IsRequired();

        builder.Property(ti => ti.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(ti => ti.Description)
            .HasMaxLength(1000);

        builder.Property(ti => ti.Priority)
            .IsRequired()
            .HasConversion(
                v => v.ToString(),
                v => (TaskPriority)Enum.Parse(typeof(TaskPriority), v));

        builder.Property(ti => ti.Status)
            .IsRequired()
            .HasConversion(
                v => v.ToString(),
                v => (TaskItemStatus)Enum.Parse(typeof(TaskItemStatus), v));

        builder.Property(ti => ti.DueDate);

        builder.Property(ti => ti.EstimatedHours)
            .HasColumnType("decimal(5,2)");

        // Relationships
        builder.HasOne(ti => ti.WorkShiftPlan)
            .WithMany(wsp => wsp.TaskItems)
            .HasForeignKey(ti => ti.WorkShiftPlanId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
