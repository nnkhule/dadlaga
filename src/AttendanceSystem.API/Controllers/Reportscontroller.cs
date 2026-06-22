using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AttendIQ.Api.Services;  // ← ADD THIS LINE


namespace AttendIQ.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Admin,HR")]
public class ReportsController(IEmployeeReportService reportService) : ControllerBase
{
    [HttpGet("employee/{employeeId:guid}")]
    public async Task<IActionResult> GetEmployeeReport(
        Guid employeeId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct)
    {
        if (to < from) return BadRequest("Дуусах огноо эхлэх огнооноос өмнө байж болохгүй.");
        if (to.ToDateTime(TimeOnly.MinValue) - from.ToDateTime(TimeOnly.MinValue) > TimeSpan.FromDays(366))
            return BadRequest("Хамгийн ихдээ 1 жилийн тайлан гаргаж болно.");

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
        var bytes = await reportService.ExportPdfAsync(employeeId, from, to, ct);
        if (bytes is null) return NotFound();
        return File(bytes, "application/pdf", $"report_{employeeId}_{from:yyyyMM}.pdf");
    }

    [HttpGet("employee/{employeeId:guid}/excel")]
    public async Task<IActionResult> ExportExcel(
        Guid employeeId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct)
    {
        var bytes = await reportService.ExportExcelAsync(employeeId, from, to, ct);
        if (bytes is null) return NotFound();
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"report_{employeeId}_{from:yyyyMM}.xlsx");
    }
}