using AttendanceSystem.Domain.Common;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Fine-grained permission for RBAC.
/// </summary>
public class Permission : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ICollection<RolePermission> RolePermissions { get; private set; } = [];
}
