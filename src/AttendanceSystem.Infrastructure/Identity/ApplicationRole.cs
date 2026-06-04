using Microsoft.AspNetCore.Identity;

namespace AttendanceSystem.Infrastructure.Identity;

/// <summary>
/// Application role with description for RBAC.
/// </summary>
public class ApplicationRole : IdentityRole
{
    public string? Description { get; set; }
}
