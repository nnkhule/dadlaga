using AttendanceSystem.Domain.Common;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Organizational department with optional parent and department head.
/// </summary>
public class Department : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public Guid? ParentDepartmentId { get; private set; }
    public Guid? HeadEmployeeId { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Guid? DefaultWorkScheduleId { get; private set; }

    public Department? ParentDepartment { get; private set; }
    public ICollection<Employee> Employees { get; private set; } = [];

    private Department() { }

    public static Department Create(string name, Guid? parentId = null, Guid? headEmployeeId = null)
        => new() { Name = name, ParentDepartmentId = parentId, HeadEmployeeId = headEmployeeId };

    public void Update(string name, Guid? headEmployeeId, Guid? defaultWorkScheduleId)
    {
        Name = name;
        HeadEmployeeId = headEmployeeId;
        DefaultWorkScheduleId = defaultWorkScheduleId;
        SetUpdated();
    }

    public void Deactivate() { IsActive = false; SetUpdated(); }
}
