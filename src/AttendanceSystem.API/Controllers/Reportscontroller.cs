using AttendanceSystem.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendIQ.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Admin,SuperAdmin,HRManager,HR,Manager")]
public class ReportsController(IEmployeeReportService reportService) : ControllerBase
{
    [HttpGet("employee/{employeeId:guid}")]
    public async Task<IActionResult> GetEmployeeReport(
        Guid employeeId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct)
    {
        var dateError = ValidateDateRange(from, to);
        if (dateError is not null) return BadRequest(dateError);

        var report = await reportService.GetAsync(employeeId, from, to, ct);
        if (report is null) return NotFound();
        return Ok(report);
    }

    [HttpGet("employee/{employeeId:guid}/pdf")]
    public async Task<IActionResult> ExportPdf(
        Guid employeeId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct)
    {
        var dateError = ValidateDateRange(from, to);
        if (dateError is not null) return BadRequest(dateError);

        var bytes = await reportService.ExportPdfAsync(employeeId, from, to, ct);
        if (bytes is null) return NotFound();
        return File(bytes, "text/plain; charset=utf-8", $"report_{employeeId}_{from:yyyyMM}.txt");
    }

    [HttpGet("employee/{employeeId:guid}/excel")]
    public async Task<IActionResult> ExportExcel(
        Guid employeeId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct)
    {
        var dateError = ValidateDateRange(from, to);
        if (dateError is not null) return BadRequest(dateError);

        var bytes = await reportService.ExportExcelAsync(employeeId, from, to, ct);
        if (bytes is null) return NotFound();
        return File(bytes, "text/csv; charset=utf-8", $"report_{employeeId}_{from:yyyyMM}.csv");
    }

    private static string? ValidateDateRange(DateOnly from, DateOnly to)
    {
        if (to < from) return "End date cannot be before start date.";
        return to.ToDateTime(TimeOnly.MinValue) - from.ToDateTime(TimeOnly.MinValue) > TimeSpan.FromDays(366)
            ? "The report range cannot exceed one year."
            : null;
    }
}
