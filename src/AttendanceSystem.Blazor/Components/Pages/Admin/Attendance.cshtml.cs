using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AttendanceSystem.AdminPanel.Pages;

public class AttendanceModel : PageModel
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    public AttendanceModel(IHttpClientFactory http, IConfiguration config) { _http = http; _config = config; }

    public List<AttendanceDto> Records { get; set; } = [];
    public string? ApiError { get; set; }
    [BindProperty(SupportsGet = true)] public string? Date { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/login");

        var targetDate = Date ?? DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");
        try
        {
            var client = _http.CreateClient("API");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var apiBase = _config["ApiBaseUrl"] ?? "https://localhost:7000";
            var result = await client.GetFromJsonAsync<PagedResult<AttendanceDto>>(
                $"{apiBase}/api/attendance?date={targetDate}&pageSize=50", ct);
            Records = result?.Items ?? [];
        }
        catch (Exception ex) { ApiError = ex.Message; }
        return Page();
    }

    public record AttendanceDto(string EmployeeName, string? CheckIn, string? CheckOut, string Status, int? LateMinutes, string Date);
    public record PagedResult<T>(List<T> Items, int TotalCount);
}
