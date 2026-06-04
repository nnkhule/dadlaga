namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Join entity between roles and permissions.
/// </summary>
public class RolePermission
{
    public string RoleId { get; set; } = string.Empty;
    public Guid PermissionId { get; set; }
    public Permission? Permission { get; set; }
}
