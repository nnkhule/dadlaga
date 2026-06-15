using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AttendanceSystem.AdminPanel.Pages;

public class EmployeesModel : PageModel
{
    private readonly IHttpClientFactory _http;
    private readonly IConfiguration _config;

    public EmployeesModel(IHttpClientFactory http, IConfiguration config)
    {
        _http   = http;
        _config = config;
    }

    public List<EmployeeDto> Employees { get; set; } = [];
    public int TotalCount { get; set; }
    public string? ApiError { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;   // "Page" нэр PageModel.Page()-тай мөргөлддөг тул PageNumber болгов

    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var token = HttpContext.Session.GetString("AccessToken");
        if (string.IsNullOrEmpty(token)) return RedirectToPage("/login");

        try
        {
            var client = _http.CreateClient("API");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
            var apiBase = _config["ApiBaseUrl"] ?? "https://localhost:7000";

            var url = $"{apiBase}/api/employees?pageNumber={PageNumber}&pageSize=20";
            if (!string.IsNullOrEmpty(Search))
                url += $"&search={Uri.EscapeDataString(Search)}";

            var result = await client.GetFromJsonAsync<PagedResult<EmployeeDto>>(url, ct);
            Employees  = result?.Items ?? [];
            TotalCount = result?.TotalCount ?? 0;
        }
        catch (Exception ex) { ApiError = ex.Message; }

        return Page();
    }

    public record EmployeeDto(
        Guid    Id,
        string  FullName,
        string? Email,
        string? DepartmentName,
        string? Position,
        bool    IsActive);

    public record PagedResult<T>(List<T> Items, int TotalCount);
}
