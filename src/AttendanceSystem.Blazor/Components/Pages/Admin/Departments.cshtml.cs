using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AttendanceSystem.AdminPanel.Pages;

public class DepartmentsModel : PageModel
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;
    public DepartmentsModel(IHttpClientFactory http, IConfiguration config) { _http = http; _config = config; }

    public List<DeptDto> Departments { get; set; } = [];
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
            Departments = await client.GetFromJsonAsync<List<DeptDto>>($"{apiBase}/api/departments", ct) ?? [];
        }
        catch (Exception ex) { ApiError = ex.Message; }
        return Page();
    }

    public record DeptDto(Guid Id, string Name, string? Description, int EmployeeCount);
}
