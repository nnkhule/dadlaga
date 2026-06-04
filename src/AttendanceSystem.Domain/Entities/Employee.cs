using AttendanceSystem.Domain.Common;
using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Employee master data linked to identity user and work configuration.
/// </summary>
public class Employee : BaseEntity
{
    
    public string EmployeeCode { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? Phone { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public Guid DepartmentId { get; private set; }
    public Guid? PositionId { get; private set; }
    public Guid WorkScheduleId { get; private set; }
    public Guid OfficeLocationId { get; private set; }
    public string? ProfilePhotoUrl { get; private set; }
    public DateOnly HireDate { get; private set; }
    public ContractType ContractType { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? UserId { get; private set; }

    public Department? Department { get; private set; }
    public WorkSchedule? WorkSchedule { get; private set; }
    public OfficeLocation? OfficeLocation { get; private set; }
    public ICollection<AttendanceRecord> AttendanceRecords { get; private set; } = [];

    private Employee() { }

    public static Employee Create(
        string employeeCode,
        string fullName,
        string email,
        Guid departmentId,
        Guid workScheduleId,
        Guid officeLocationId,
        DateOnly hireDate,
        ContractType contractType,
        DateOnly? dateOfBirth = null,
        string? phone = null,
        string? userId = null)
    {
        return new Employee
        {
            EmployeeCode = employeeCode,
            FullName = fullName,
            Email = email,
            Phone = phone,
            DateOfBirth = dateOfBirth,
            DepartmentId = departmentId,
            WorkScheduleId = workScheduleId,
            OfficeLocationId = officeLocationId,
            HireDate = hireDate,
            ContractType = contractType,
            UserId = userId
        };
    }

    public void Update(string fullName, string email, string? phone, Guid departmentId,
        Guid workScheduleId, Guid officeLocationId, DateOnly? dateOfBirth)
    {
        FullName = fullName;
        Email = email;
        Phone = phone;
        DepartmentId = departmentId;
        WorkScheduleId = workScheduleId;
        OfficeLocationId = officeLocationId;
        DateOfBirth = dateOfBirth;
        SetUpdated();
    }

    public void Deactivate() { IsActive = false; SetUpdated(); }
    public void LinkUser(string userId) { UserId = userId; SetUpdated(); }
}
