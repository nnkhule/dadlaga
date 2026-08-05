using AttendanceSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceSystem.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for TaskComment entity.
/// </summary>
public class TaskCommentConfiguration : IEntityTypeConfiguration<TaskComment>
{
    public void Configure(EntityTypeBuilder<TaskComment> builder)
    {
        builder.ToTable("TaskComments");

        builder.HasKey(tc => tc.Id);

        builder.Property(tc => tc.TaskItemId)
            .IsRequired();

        builder.Property(tc => tc.Content)
            .IsRequired()
            .HasMaxLength(1000);

        // Relationships
        builder.HasOne(tc => tc.TaskItem)
            .WithMany(ti => ti.Comments)
            .HasForeignKey(tc => tc.TaskItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}