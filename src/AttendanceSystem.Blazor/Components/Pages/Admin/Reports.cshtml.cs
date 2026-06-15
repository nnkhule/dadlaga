using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AttendanceSystem.AdminPanel.Pages;

public class ReportsModel : PageModel
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    public ReportsModel(IHttpClientFactory http, IConfiguration config) { _http = http; _config = config; }

    public List<List<string>> ReportData { get; set; } = [];
    public List<string> ReportHeaders { get; set; } = [];
    public string ReportTitle { get; set; } = "";
    public string? ApiError { get; set; }

    public IActionResult OnGet() { 
        var t = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrEmpty(t)) return RedirectToPage("/login");
        return Page(); 
    }

    public async Task<IActionResult> OnGetMonthlyAsync(string? month, CancellationToken ct)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/login");
        ReportTitle = $"Сарын тайлан: {month}";
        ReportHeaders = ["Ажилтан", "Нийт өдөр", "Ирсэн", "Хоцорсон", "Ирээгүй", "Чөлөөтэй"];
        try
        {
            var client = _http.CreateClient("API");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var apiBase = _config["ApiBaseUrl"] ?? "https://localhost:7000";
            var result = await client.GetFromJsonAsync<List<MonthlyReportRow>>(
                $"{apiBase}/api/reports/monthly?month={month}", ct);
            ReportData = result?.Select(r => new List<string> {
                r.EmployeeName, r.WorkDays.ToString(), r.PresentDays.ToString(),
                r.LateDays.ToString(), r.AbsentDays.ToString(), r.LeaveDays.ToString()
            }).ToList() ?? [];
        }
        catch (Exception ex) { ApiError = ex.Message; }
        return Page();
    }

    public async Task<IActionResult> OnGetDepartmentAsync(string? month, CancellationToken ct)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/login");
        ReportTitle = $"Хэлтсийн тайлан: {month}";
        ReportHeaders = ["Хэлтэс", "Нийт ажилтан", "Дундаж ирц %", "Хоцролтын дундаж"];
        try
        {
            var client = _http.CreateClient("API");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var apiBase = _config["ApiBaseUrl"] ?? "https://localhost:7000";
            var result = await client.GetFromJsonAsync<List<DeptReportRow>>(
                $"{apiBase}/api/reports/departments?month={month}", ct);
            ReportData = result?.Select(r => new List<string> {
                r.DepartmentName, r.EmployeeCount.ToString(),
                $"{r.AttendanceRate:F1}%", $"{r.AvgLateMinutes:F0}мин"
            }).ToList() ?? [];
        }
        catch (Exception ex) { ApiError = ex.Message; }
        return Page();
    }

    public record MonthlyReportRow(string EmployeeName, int WorkDays, int PresentDays, int LateDays, int AbsentDays, int LeaveDays);
    public record DeptReportRow(string DepartmentName, int EmployeeCount, decimal AttendanceRate, decimal AvgLateMinutes);
}
