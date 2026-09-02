namespace AttendanceSystem.Domain;

/// <summary>
/// Application-wide role constants.
/// </summary>
public static class AppRoles
{
    // Actual role names as stored in the database
    public const string Employee = "Employee";
    public const string HrManager = "HRManager";
    public const string DepartmentHead = "DepartmentHead";
    public const string SuperAdmin = "SuperAdmin";

    // Backward-compatible aliases for existing code
    public const string Hr = HrManager;       // Maps old "HR" to new "HRManager"
    public const string Manager = DepartmentHead; // Maps old "Manager" to new "DepartmentHead"
    public const string Admin = SuperAdmin;   // Maps old "Admin" to new "SuperAdmin"

    // Composite roles (kept for compatibility)
    public const string HrOrAdmin = $"{HrManager},{Admin}";
    public const string ManagerHrOrAdmin = $"{DepartmentHead},{HrManager},{Admin}";
    public const string AdminOrSuperAdmin = $"{Admin},{SuperAdmin}";
}