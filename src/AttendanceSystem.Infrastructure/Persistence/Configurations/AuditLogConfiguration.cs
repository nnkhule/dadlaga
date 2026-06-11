using AttendanceSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceSystem.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for <see cref="AuditLog"/>.
/// </summary>
public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasIndex(x => new { x.EntityId, x.Action })
            .HasDatabaseName("IX_AuditLog_EntityId_Action")
            .IncludeProperties(x => new { x.CreatedAt, x.PerformedBy });
    }
}
