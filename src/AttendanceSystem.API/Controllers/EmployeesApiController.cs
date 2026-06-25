using System.Security.Claims;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
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

    /// <summary>
    /// Жагсаалт — хайлт, хэлтэс, төлөв (active/inactive), pagination дэмждэг.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,SuperAdmin,HRManager,HR,Manager,DepartmentHead")]
    public async Task<ActionResult<PagedResponseDto<EmployeeApiDto>>> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] bool? isActive = null,   // ← ШИНЭ: status filter (true/false/null = бүгд)
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

        if (departmentId.HasValue)
            query = query.Where(e => e.DepartmentId == departmentId.Value);

        if (isActive.HasValue)
            query = query.Where(e => e.IsActive == isActive.Value);

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
                e.DepartmentId,
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
    [Authorize(Roles = "SuperAdmin,HRManager,DepartmentHead")]
    public async Task<ActionResult<EmployeeApiDto>> Details(Guid id, CancellationToken cancellationToken)
    {
        var employee = await _db.Employees
            .AsNoTracking()
            .Include(e => e.Department)
            .Where(e => e.Id == id)
            .Select(e => ToEmployeeDto(e.Id, e.EmployeeCode, e.FullName, e.Email, e.Phone,
                e.DepartmentId, e.Department == null ? null : e.Department.Name, e.HireDate, e.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpPost]
    [Authorize(Roles = "SuperAdmin,HRManager")]
    public async Task<ActionResult<EmployeeApiDto>> Create([FromBody] EmployeeFormApiDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeCode) || string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "Ажилтны код, нэр, имэйл заавал шаардлагатай." });

        if (!request.DepartmentId.HasValue)
            return BadRequest(new { message = "Хэлтэс сонгох шаардлагатай." });

        // Код давхардал шалгах
        var codeExists = await _db.Employees.AsNoTracking()
            .AnyAsync(e => e.EmployeeCode == request.EmployeeCode.Trim(), cancellationToken);
        if (codeExists)
            return BadRequest(new { message = $"'{request.EmployeeCode}' код бүхий ажилтан аль хэдийн бүртгэлтэй байна." });

        // Имэйл давхардал шалгах
        var emailExists = await _db.Employees.AsNoTracking()
            .AnyAsync(e => e.Email == request.Email.Trim(), cancellationToken);
        if (emailExists)
            return BadRequest(new { message = $"'{request.Email}' имэйл хаягаар бүртгэлтэй ажилтан байна." });

        var scheduleId = await _db.WorkSchedules.AsNoTracking().OrderBy(w => w.Name).Select(w => w.Id).FirstOrDefaultAsync(cancellationToken);
        var officeId = await _db.OfficeLocations.AsNoTracking().OrderBy(o => o.Name).Select(o => o.Id).FirstOrDefaultAsync(cancellationToken);
        if (scheduleId == Guid.Empty || officeId == Guid.Empty)
            return BadRequest(new { message = "Эхлээд ажлын хуваарь болон оффисын байршил үүсгэх шаардлагатай." });

        var employee = Employee.Create(
            request.EmployeeCode.Trim(),
            request.FullName.Trim(),
            request.Email.Trim(),
            request.DepartmentId.Value,
            scheduleId,
            officeId,
            request.HireDate ?? DateOnly.FromDateTime(DateTime.Now),
            ContractType.FullTime,
            request.DateOfBirth,
            request.Phone);

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Details), new { id = employee.Id }, await BuildEmployeeDto(employee.Id, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,HRManager")]
    public async Task<ActionResult<EmployeeApiDto>> Update(Guid id, [FromBody] EmployeeFormApiDto request, CancellationToken cancellationToken)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (employee is null)
            return NotFound();

        // Имэйл давхардал шалгах (өөрөөсөө бусад ажилтантай)
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var emailTaken = await _db.Employees.AsNoTracking()
                .AnyAsync(e => e.Id != id && e.Email == request.Email.Trim(), cancellationToken);
            if (emailTaken)
                return BadRequest(new { message = $"'{request.Email}' имэйл хаягаар бүртгэлтэй өөр ажилтан байна." });
        }

        employee.Update(
            string.IsNullOrWhiteSpace(request.FullName) ? employee.FullName : request.FullName.Trim(),
            string.IsNullOrWhiteSpace(request.Email) ? employee.Email : request.Email.Trim(),
            request.Phone,
            request.DepartmentId ?? employee.DepartmentId,
            employee.WorkScheduleId,
            employee.OfficeLocationId,
            request.DateOfBirth);

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(await BuildEmployeeDto(id, cancellationToken));
    }

    /// <summary>
    /// Soft delete — ажилтныг идэвхгүй болгоно (IsActive = false).
    /// AttendanceRecords / LeaveRequests-тэй FK холбоотой тул физикийн устгал хийхгүй.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "SuperAdmin,HRManager")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (employee is null)
            return NotFound(new { message = "Ажилтан олдсонгүй." });

        if (!employee.IsActive)
            return Ok(new { message = "Ажилтан өмнө нь идэвхгүй болсон байна." });

        employee.Deactivate();
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Ажилтныг идэвхгүй болгосон." });
    }

    /// <summary>
    /// Идэвхгүй болгосон ажилтныг дахин идэвхжүүлэх.
    /// </summary>
    [HttpPost("{id:guid}/reactivate")]
    [Authorize(Roles = "SuperAdmin,HRManager")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (employee is null)
            return NotFound(new { message = "Ажилтан олдсонгүй." });

        employee.Reactivate();
        await _db.SaveChangesAsync(cancellationToken);

        return Ok(new { message = "Ажилтныг идэвхжүүллээ." });
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

    private async Task<EmployeeApiDto?> BuildEmployeeDto(Guid id, CancellationToken cancellationToken)
        => await _db.Employees.AsNoTracking().Include(e => e.Department)
            .Where(e => e.Id == id)
            .Select(e => ToEmployeeDto(e.Id, e.EmployeeCode, e.FullName, e.Email, e.Phone,
                e.DepartmentId, e.Department == null ? null : e.Department.Name, e.HireDate, e.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

    private static EmployeeApiDto ToEmployeeDto(Guid id, string code, string name, string email, string? phone, Guid departmentId, string? department, DateOnly hireDate, bool isActive)
        => new(id, code, name, email, phone, departmentId, department, department, null, hireDate, isActive ? "Active" : "Inactive", isActive);
}

public sealed record EmployeeApiDto(
    Guid Id,
    string EmployeeCode,
    string FullName,
    string Email,
    string? Phone,
    Guid DepartmentId,
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
public sealed record EmployeeFormApiDto(
    string? EmployeeCode,
    string? FullName,
    string? Email,
    string? Phone,
    Guid? DepartmentId,
    string? Position,
    DateOnly? HireDate,
    DateOnly? DateOfBirth);
