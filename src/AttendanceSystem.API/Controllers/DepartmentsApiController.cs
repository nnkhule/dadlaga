using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.API.Controllers;

[ApiController]
[Route("api/departments")]
[Authorize]
public sealed class DepartmentsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public DepartmentsApiController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<PagedResponseDto<DepartmentApiDto>>> List(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.Departments.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(d => d.Name.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(d => d.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DepartmentApiDto(
                d.Id,
                d.Name,
                null,
                d.HeadEmployeeId == null
                    ? null
                    : _db.Employees.Where(e => e.Id == d.HeadEmployeeId.Value).Select(e => e.FullName).FirstOrDefault(),
                d.Employees.Count,
                d.IsActive))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponseDto<DepartmentApiDto>(items, pageNumber, pageSize, total));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DepartmentApiDto>> Details(Guid id, CancellationToken cancellationToken)
    {
        var department = await _db.Departments
            .AsNoTracking()
            .Where(d => d.Id == id)
            .Select(d => new DepartmentApiDto(
                d.Id,
                d.Name,
                null,
                d.HeadEmployeeId == null
                    ? null
                    : _db.Employees.Where(e => e.Id == d.HeadEmployeeId.Value).Select(e => e.FullName).FirstOrDefault(),
                d.Employees.Count,
                d.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        return department is null ? NotFound() : Ok(department);
    }

    [HttpPost]
    public async Task<ActionResult<DepartmentApiDto>> Create([FromBody] DepartmentFormApiDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Department name is required." });

        var department = Department.Create(request.Name.Trim());
        _db.Departments.Add(department);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(Details), new { id = department.Id }, new DepartmentApiDto(
            department.Id,
            department.Name,
            null,
            null,
            0,
            department.IsActive));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DepartmentApiDto>> Update(Guid id, [FromBody] DepartmentFormApiDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Department name is required." });

        var department = await _db.Departments.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (department is null)
            return NotFound();

        department.Update(request.Name.Trim(), null, null);
        await _db.SaveChangesAsync(cancellationToken);

        return await Details(id, cancellationToken);
    }
}

public sealed record DepartmentApiDto(Guid Id, string Name, string? Description, string? HeadEmployeeName, int EmployeeCount, bool IsActive);
public sealed record DepartmentFormApiDto(string Name, string? Description);
