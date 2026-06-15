using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AttendanceSystem.AdminPanel.Pages;

public class SettingsModel : PageModel
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    public SettingsModel(IHttpClientFactory http, IConfiguration config) { _http = http; _config = config; }

    public int GraceMinutes { get; set; } = 10;
    public string WorkStartTime { get; set; } = "09:00";
    public bool Saved { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/login");
        try
        {
            var client = _http.CreateClient("API");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var apiBase = _config["ApiBaseUrl"] ?? "https://localhost:7000";
            var s = await client.GetFromJsonAsync<SettingsDto>($"{apiBase}/api/settings/attendance-rules", ct);
            if (s is not null) { GraceMinutes = s.GraceMinutes; WorkStartTime = s.WorkStartTime; }
        }
        catch { }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int graceMinutes, string workStartTime, CancellationToken ct)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/login");
        GraceMinutes = graceMinutes; WorkStartTime = workStartTime;
        try
        {
            var client = _http.CreateClient("API");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var apiBase = _config["ApiBaseUrl"] ?? "https://localhost:7000";
            await client.PutAsJsonAsync($"{apiBase}/api/settings/attendance-rules",
                new { GraceMinutes = graceMinutes, WorkStartTime = workStartTime }, ct);
            Saved = true;
        }
        catch { }
        return Page();
    }

    public record SettingsDto(int GraceMinutes, string WorkStartTime);
}
