using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Infrastructure.Persistence;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.API.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public sealed class ReportsApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ReportsApiController(ApplicationDbContext db) => _db = db;

    [HttpGet("attendance")]
    public async Task<ActionResult<PagedResponseDto<Dictionary<string, object?>>>> Attendance(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.AttendanceRecords
            .AsNoTracking()
            .Include(a => a.Employee)
            .ThenInclude(e => e!.Department)
            .Where(a => a.Date >= from && a.Date <= to)
            .OrderByDescending(a => a.Date)
            .ThenBy(a => a.Employee == null ? null : a.Employee.FullName);

        var total = await query.CountAsync(cancellationToken);
        var data = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(a => new
            {
                a.Date,
                Employee = a.Employee == null ? null : a.Employee.FullName,
                Department = a.Employee == null || a.Employee.Department == null ? null : a.Employee.Department.Name,
                a.CheckInTime,
                a.CheckOutTime,
                Status = a.Status.ToString(),
                a.LateMinutes,
                a.OvertimeHours
            })
            .ToListAsync(cancellationToken);
        var rows = data.Select(a => new Dictionary<string, object?>
        {
            ["Date"] = a.Date,
            ["Employee"] = a.Employee,
            ["Department"] = a.Department,
            ["CheckInTime"] = a.CheckInTime,
            ["CheckOutTime"] = a.CheckOutTime,
            ["Status"] = a.Status,
            ["LateMinutes"] = a.LateMinutes,
            ["OvertimeHours"] = a.OvertimeHours
        }).ToList();

        return Ok(new PagedResponseDto<Dictionary<string, object?>>(rows, pageNumber, pageSize, total));
    }

    [HttpGet("employees")]
    public async Task<ActionResult<PagedResponseDto<Dictionary<string, object?>>>> Employees(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Employees.AsNoTracking().Include(e => e.Department).OrderBy(e => e.FullName);
        var total = await query.CountAsync(cancellationToken);
        var data = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(e => new
            {
                e.EmployeeCode,
                e.FullName,
                e.Email,
                e.Phone,
                Department = e.Department == null ? null : e.Department.Name,
                e.HireDate,
                e.IsActive
            })
            .ToListAsync(cancellationToken);
        var rows = data.Select(e => new Dictionary<string, object?>
        {
            ["EmployeeCode"] = e.EmployeeCode,
            ["FullName"] = e.FullName,
            ["Email"] = e.Email,
            ["Phone"] = e.Phone,
            ["Department"] = e.Department,
            ["HireDate"] = e.HireDate,
            ["IsActive"] = e.IsActive
        }).ToList();

        return Ok(new PagedResponseDto<Dictionary<string, object?>>(rows, pageNumber, pageSize, total));
    }

    [HttpGet("departments")]
    public async Task<ActionResult<PagedResponseDto<Dictionary<string, object?>>>> Departments(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = _db.Departments.AsNoTracking().OrderBy(d => d.Name);
        var total = await query.CountAsync(cancellationToken);
        var data = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(d => new { d.Name, EmployeeCount = d.Employees.Count, d.IsActive })
            .ToListAsync(cancellationToken);
        var rows = data.Select(d => new Dictionary<string, object?>
        {
            ["Name"] = d.Name,
            ["EmployeeCount"] = d.EmployeeCount,
            ["IsActive"] = d.IsActive
        }).ToList();

        return Ok(new PagedResponseDto<Dictionary<string, object?>>(rows, pageNumber, pageSize, total));
    }

    [HttpGet("summary")]
    public async Task<ActionResult<IReadOnlyList<DepartmentReportSummaryApiDto>>> Summary(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
    {
        var departments = await _db.Departments.AsNoTracking()
            .Select(d => new
            {
                d.Id,
                d.Name,
                Present = d.Employees.SelectMany(e => e.AttendanceRecords).Count(a => a.Date >= from && a.Date <= to && a.Status != AttendanceStatus.Absent && a.Status != AttendanceStatus.OnLeave),
                Late = d.Employees.SelectMany(e => e.AttendanceRecords).Count(a => a.Date >= from && a.Date <= to && (a.Status == AttendanceStatus.Late || a.LateMinutes > 0)),
                Leave = d.Employees.SelectMany(e => e.AttendanceRecords).Count(a => a.Date >= from && a.Date <= to && a.Status == AttendanceStatus.OnLeave)
            })
            .Select(d => new DepartmentReportSummaryApiDto(d.Id, d.Name, d.Present, d.Late, d.Leave))
            .ToListAsync(cancellationToken);

        return Ok(departments);
    }

    [HttpGet("{reportType}/export/excel")]
    public async Task<IActionResult> ExportExcel(string reportType, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
        => File(await BuildCsvAsync(reportType, from, to, cancellationToken), "text/csv", $"{reportType}-{from:yyyyMMdd}-{to:yyyyMMdd}.csv");

    [HttpGet("{reportType}/export/pdf")]
    public async Task<IActionResult> ExportPdf(string reportType, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
        => File(await BuildCsvAsync(reportType, from, to, cancellationToken), "text/csv", $"{reportType}-{from:yyyyMMdd}-{to:yyyyMMdd}.csv");

    private async Task<byte[]> BuildCsvAsync(string reportType, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var rows = reportType.ToLowerInvariant() switch
        {
            "employees" => (await Employees(1, 10000, cancellationToken)).Value?.Items ?? [],
            "departments" => (await Departments(1, 10000, cancellationToken)).Value?.Items ?? [],
            _ => (await Attendance(from, to, 1, 10000, cancellationToken)).Value?.Items ?? []
        };

        var columns = rows.SelectMany(r => r.Keys).Distinct().ToList();
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", columns.Select(EscapeCsv)));
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", columns.Select(c => EscapeCsv(row.TryGetValue(c, out var value) ? value?.ToString() : null))));
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static string EscapeCsv(string? value)
    {
        value ??= string.Empty;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}

public sealed record DepartmentReportSummaryApiDto(Guid DepartmentId, string DepartmentName, int Present, int Late, int Leave);
