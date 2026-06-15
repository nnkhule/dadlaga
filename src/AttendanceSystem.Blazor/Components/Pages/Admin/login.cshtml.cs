using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AttendanceSystem.AdminPanel.Pages;

public class LoginModel : PageModel
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;

    public LoginModel(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ErrorMessage { get; set; }
    public bool SessionExpired => Request.Query["expired"] == "1";

    public class InputModel
    {
        [Required(ErrorMessage = "И-мэйл хаяг оруулна уу")]
        [EmailAddress(ErrorMessage = "Буруу и-мэйл формат")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Нууц үг оруулна уу")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }

    public IActionResult OnGet()
    {
        if (HttpContext.Session.GetString("AccessToken") is not null)
            return RedirectToPage("/Dashboard");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        try
        {
            var client  = _httpClientFactory.CreateClient("API");
            var apiBase = _config["ApiBaseUrl"] ?? "https://localhost:7000";

            var response = await client.PostAsJsonAsync(
                $"{apiBase}/api/auth/login",
                new { Input.Email, Input.Password });

            if (!response.IsSuccessStatusCode)
            {
                ErrorMessage = "И-мэйл эсвэл нууц үг буруу байна.";
                return Page();
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (token is null)
            {
                ErrorMessage = "Серверээс хариу алдаатай ирлээ.";
                return Page();
            }

            // Admin role шалгах — JWT payload base64 decode (System.IdentityModel ашиглахгүй)
            if (!HasAdminRole(token.AccessToken))
            {
                ErrorMessage = "Танд admin эрх байхгүй. Зөвхөн SuperAdmin, Admin, HRManager нэвтэрч болно.";
                return Page();
            }

            HttpContext.Session.SetString("AccessToken",  token.AccessToken);
            HttpContext.Session.SetString("RefreshToken", token.RefreshToken);
            HttpContext.Session.SetString("ExpiresAt",    token.ExpiresAt.ToString("o"));
            HttpContext.Session.SetString("AdminEmail",   Input.Email);

            if (Input.RememberMe)
                Response.Cookies.Append("RememberMe", "1",
                    new CookieOptions { Expires = DateTimeOffset.UtcNow.AddDays(30), HttpOnly = true });

            return RedirectToPage("/Dashboard");
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"API сервертэй холбогдож чадсангүй: {ex.Message}";
            return Page();
        }
        catch (TaskCanceledException)
        {
            ErrorMessage = "API сервер хугацаандаа хариу өгсөнгүй. Дахин оролдоно уу.";
            return Page();
        }
    }

    /// <summary>
    /// JWT-ийн payload хэсгийг base64 decode хийж role шалгана.
    /// System.IdentityModel.Tokens.Jwt package шаардахгүй.
    /// </summary>
    private static bool HasAdminRole(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2) return false;

            // Base64Url → Base64 padding засах
            var payload = parts[1];
            payload = payload.Replace('-', '+').Replace('_', '/');
            var pad = payload.Length % 4;
            if (pad == 2) payload += "==";
            else if (pad == 3) payload += "=";

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // ASP.NET Identity роль claim
            var roleClaimKey = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";

            // Массив байж болно
            if (root.TryGetProperty(roleClaimKey, out var roleProp))
                return ContainsAdminRole(roleProp);

            // Зарим token-д "role" гэж богинохон байдаг
            if (root.TryGetProperty("role", out var roleProp2))
                return ContainsAdminRole(roleProp2);

            return false;
        }
        catch { return false; }
    }

    private static bool ContainsAdminRole(JsonElement el)
    {
        var adminRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "SuperAdmin", "Admin", "HRManager" };

        if (el.ValueKind == JsonValueKind.String)
            return adminRoles.Contains(el.GetString() ?? "");

        if (el.ValueKind == JsonValueKind.Array)
            return el.EnumerateArray().Any(r => adminRoles.Contains(r.GetString() ?? ""));

        return false;
    }

    private record TokenResponse(
        string   AccessToken,
        string   RefreshToken,
        DateTime ExpiresAt,
        Guid?    EmployeeId);
}
