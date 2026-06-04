using Microsoft.AspNetCore.Identity;

namespace AttendanceSystem.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity application user.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public Guid? EmployeeId { get; set; }
    public string? FullName { get; set; }
}
