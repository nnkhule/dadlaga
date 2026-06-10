using System.Security.Claims;
using AttendanceSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.API.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize]
public sealed class EmployeesApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public EmployeesApiController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<PagedResponseDto<EmployeeApiDto>>> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Employees.AsNoTracking().Include(e => e.Department).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e =>
                e.EmployeeCode.Contains(term) ||
                e.FullName.Contains(term) ||
                e.Email.Contains(term) ||
                (e.Department != null && e.Department.Name.Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(e => e.FullName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EmployeeApiDto(
                e.Id,
                e.EmployeeCode,
                e.FullName,
                e.Email,
                e.Phone,
                e.Department == null ? null : e.Department.Name,
                e.Department == null ? null : e.Department.Name,
                null,
                e.HireDate,
                e.IsActive ? "Active" : "Inactive",
                e.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponseDto<EmployeeApiDto>(items, pageNumber, pageSize, total));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeApiDto>> Details(Guid id, CancellationToken cancellationToken)
    {
        var employee = await _db.Employees
            .AsNoTracking()
            .Include(e => e.Department)
            .Where(e => e.Id == id)
            .Select(e => ToEmployeeDto(e.Id, e.EmployeeCode, e.FullName, e.Email, e.Phone,
                e.Department == null ? null : e.Department.Name, e.HireDate, e.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpGet("me")]
    public async Task<ActionResult<EmployeeProfileApiDto>> Me(CancellationToken cancellationToken)
    {
        var employeeId = GetEmployeeId();
        if (employeeId is null)
            return Unauthorized(new { message = "Employee profile is not linked to this user." });

        var employee = await _db.Employees
            .AsNoTracking()
            .Include(e => e.Department)
            .Where(e => e.Id == employeeId.Value)
            .Select(e => new EmployeeProfileApiDto(
                e.Id,
                e.ProfilePhotoUrl,
                e.EmployeeCode,
                e.FullName,
                e.Email,
                e.Phone,
                e.DateOfBirth,
                e.Department == null ? null : e.Department.Name,
                e.Department == null ? null : e.Department.Name,
                null,
                e.HireDate,
                e.IsActive ? "Active" : "Inactive",
                e.IsActive ? "Active" : "Inactive",
                e.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpPut("me")]
    public async Task<ActionResult<EmployeeProfileApiDto>> UpdateMe(
        [FromBody] UpdateEmployeeProfileApiDto request,
        CancellationToken cancellationToken)
    {
        var employeeId = GetEmployeeId();
        if (employeeId is null)
            return Unauthorized(new { message = "Employee profile is not linked to this user." });

        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == employeeId.Value, cancellationToken);
        if (employee is null)
            return NotFound();

        employee.Update(
            request.FullName,
            request.Email,
            request.Phone,
            employee.DepartmentId,
            employee.WorkScheduleId,
            employee.OfficeLocationId,
            request.DateOfBirth);

        await _db.SaveChangesAsync(cancellationToken);
        return await Me(cancellationToken);
    }

    private Guid? GetEmployeeId()
    {
        var claim = User.FindFirstValue("employee_id");
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private static EmployeeApiDto ToEmployeeDto(Guid id, string code, string name, string email, string? phone, string? department, DateOnly hireDate, bool isActive)
        => new(id, code, name, email, phone, department, department, null, hireDate, isActive ? "Active" : "Inactive", isActive);
}

public sealed record EmployeeApiDto(
    Guid Id,
    string EmployeeCode,
    string FullName,
    string Email,
    string? Phone,
    string? Department,
    string? DepartmentName,
    string? Position,
    DateOnly HireDate,
    string? Status,
    bool IsActive);

public sealed record EmployeeProfileApiDto(
    Guid Id,
    string? ProfilePhotoUrl,
    string EmployeeCode,
    string FullName,
    string Email,
    string? Phone,
    DateOnly? DateOfBirth,
    string? Department,
    string? DepartmentName,
    string? Position,
    DateOnly HireDate,
    string? EmploymentStatus,
    string? Status,
    bool IsActive);

public sealed record UpdateEmployeeProfileApiDto(string FullName, string Email, string? Phone, DateOnly? DateOfBirth);
