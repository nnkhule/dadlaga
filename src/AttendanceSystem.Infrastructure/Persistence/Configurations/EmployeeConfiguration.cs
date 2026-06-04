using AttendanceSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AttendanceSystem.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF configuration for <see cref="Employee"/>.
/// </summary>
public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("Employees");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.EmployeeCode).IsUnique();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.HasOne(x => x.Department).WithMany(d => d.Employees).HasForeignKey(x => x.DepartmentId);
        builder.HasOne(x => x.WorkSchedule).WithMany().HasForeignKey(x => x.WorkScheduleId);
        builder.HasOne(x => x.OfficeLocation).WithMany().HasForeignKey(x => x.OfficeLocationId);
    }
}
