using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AttendanceSystem.AdminPanel.Pages;

public class DashboardModel : PageModel
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;

    public DashboardModel(IHttpClientFactory http, IConfiguration config)
    {
        _http   = http;
        _config = config;
    }

    public DashboardSummary? Summary { get; set; }
    public List<RecentActivity> RecentActivities { get; set; } = [];
    public string AdminEmail { get; set; } = "Admin";
    public string? ApiError { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/login");

        AdminEmail = HttpContext.Session.GetString("AdminEmail") ?? "Admin";

        try
        {
            var client  = _http.CreateClient("API");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            var apiBase = _config["ApiBaseUrl"] ?? "https://localhost:7000";

            Summary = await client.GetFromJsonAsync<DashboardSummary>(
                $"{apiBase}/api/dashboard/summary", ct);

            var activitiesResult = await client.GetFromJsonAsync<PagedResponse<RecentActivity>>(
                $"{apiBase}/api/dashboard/recent-activities?pageSize=10", ct);
            RecentActivities = activitiesResult?.Items ?? [];
        }
        catch (Exception ex)
        {
            ApiError = $"Дата татахад алдаа гарлаа: {ex.Message}";
        }

        return Page();
    }

    public IActionResult OnPostLogout()
    {
        HttpContext.Session.Clear();
        return RedirectToPage("/login");
    }

    public record DashboardSummary(
        int     TotalEmployees,
        int     ActiveEmployees,
        int     PresentToday,
        int     AbsentToday,
        int     LateEmployees,
        int     OnLeaveEmployees,
        decimal AttendanceRate,
        decimal OvertimeHours);

    public record RecentActivity(
        string   EmployeeName,
        string   Action,
        DateTime Timestamp,
        string?  Department);

    public record PagedResponse<T>(List<T> Items, int TotalCount);
}
