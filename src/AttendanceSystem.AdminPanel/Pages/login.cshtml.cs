using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
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
        // Already logged in → go to dashboard
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

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
            if (token is null)
            {
                ErrorMessage = "Серверээс хариу алдаатай ирлээ.";
                return Page();
            }

            // Store token in session
            HttpContext.Session.SetString("AccessToken",  token.AccessToken);
            HttpContext.Session.SetString("RefreshToken", token.RefreshToken);
            HttpContext.Session.SetString("ExpiresAt",    token.ExpiresAt.ToString("o"));

            // Optionally extend session lifetime for "remember me"
            if (Input.RememberMe)
                Response.Cookies.Append("RememberMe", "1",
                    new CookieOptions { Expires = DateTimeOffset.UtcNow.AddDays(30), HttpOnly = true, Secure = true });

            return RedirectToPage("/Dashboard");
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "API сервертэй холбогдож чадсангүй. Дахин оролдоно уу.";
            return Page();
        }
    }

    // Mirrors TokenResponseDto from the API
    private record TokenResponse(
        string AccessToken,
        string RefreshToken,
        DateTime ExpiresAt,
        Guid? EmployeeId);
}