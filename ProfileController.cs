using System.Security.Claims;
using AttendanceSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.API.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize]
public class ProfileController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProfileController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyProfile()
    {
        var employeeId = GetEmployeeId();
        if (employeeId is null) return BadRequest(new { message = "Employee not linked." });

        var employee = await _context.Employees
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == employeeId.Value);

        if (employee == null) return NotFound();

        var nameParts = employee.FullName.Split(' ', 2);

        return Ok(new
        {
            FirstName = nameParts.Length > 1 ? nameParts[1] : nameParts[0],
            LastName = nameParts.Length > 1 ? nameParts[0] : "",
            FullName = employee.FullName,
            employee.Email,
            Phone = employee.Phone ?? "",
            DepartmentName = employee.Department?.Name,
            Position = "Мэргэжилтэн", 
            Status = "Идэвхтэй",    // Default value as example
            DateOfBirth = employee.DateOfBirth,
            employee.HireDate,
            ProfilePhotoUrl = (string?)null
        });
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateProfileRequest request)
    {
        var employeeId = GetEmployeeId();
        if (employeeId is null) return BadRequest(new { message = "Employee not linked." });

        var employee = await _context.Employees.FindAsync(employeeId.Value);
        if (employee == null) return NotFound();

        var newFullName = $"{request.LastName} {request.FirstName}".Trim();

        // Зөвхөн засах боломжтой талбаруудыг шинэчилнэ
        employee.Update(
            string.IsNullOrWhiteSpace(newFullName) ? employee.FullName : newFullName,
            request.Email ?? employee.Email,
            request.Phone ?? employee.Phone,
            employee.DepartmentId,
            employee.WorkScheduleId,
            employee.OfficeLocationId,
            request.DateOfBirth ?? employee.DateOfBirth);

        await _context.SaveChangesAsync();
        return Ok(new { message = "Profile updated successfully" });
    }

    private Guid? GetEmployeeId()
    {
        var claim = User.FindFirstValue("employee_id");
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    public record UpdateProfileRequest(
        string? FirstName,
        string? FirstName,
        string? LastName,
        string? Email,
        string? Phone,
        DateTime? DateOfBirth);
}