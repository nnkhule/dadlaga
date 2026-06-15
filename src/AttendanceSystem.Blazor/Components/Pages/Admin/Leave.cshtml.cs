using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AttendanceSystem.AdminPanel.Pages;

public class LeaveModel : PageModel
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    public LeaveModel(IHttpClientFactory http, IConfiguration config) { _http = http; _config = config; }

    public List<LeaveDto> Leaves { get; set; } = [];
    public string? ApiError { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/login");
        try
        {
            var client = _http.CreateClient("API");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var apiBase = _config["ApiBaseUrl"] ?? "https://localhost:7000";
            var result = await client.GetFromJsonAsync<PagedResult<LeaveDto>>(
                $"{apiBase}/api/leave?pageSize=30", ct);
            Leaves = result?.Items ?? [];
        }
        catch (Exception ex) { ApiError = ex.Message; }
        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id, CancellationToken ct)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/login");
        var client = _http.CreateClient("API");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var apiBase = _config["ApiBaseUrl"] ?? "https://localhost:7000";
        await client.PostAsync($"{apiBase}/api/leave/{id}/approve", null, ct);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id, CancellationToken ct)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/login");
        var client = _http.CreateClient("API");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var apiBase = _config["ApiBaseUrl"] ?? "https://localhost:7000";
        await client.PostAsync($"{apiBase}/api/leave/{id}/reject", null, ct);
        return RedirectToPage();
    }

    public record LeaveDto(Guid Id, string EmployeeName, string LeaveType, string StartDate, string EndDate, string Status, string? Reason);
    public record PagedResult<T>(List<T> Items, int TotalCount);
}
