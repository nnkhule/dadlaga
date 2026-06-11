using AttendanceSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceSystem.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for <see cref="RefreshToken"/>.
/// </summary>
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.Property(x => x.Token).HasMaxLength(512);
        builder.HasIndex(x => x.Token).HasDatabaseName("IX_RefreshTokens_Token");
        builder.HasIndex(x => x.UserId).HasDatabaseName("IX_RefreshTokens_UserId");
    }
}
