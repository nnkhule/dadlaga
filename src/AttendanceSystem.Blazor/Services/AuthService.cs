using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AttendanceSystem.Blazor.Models;
using Microsoft.JSInterop;

namespace AttendanceSystem.Blazor.Services;

public sealed class AuthService
{
    private const string TokenKey = "attendance.accessToken";
    private const string RefreshTokenKey = "attendance.refreshToken";
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;

    public AuthService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    public async Task<(bool Success, string? Error, string TargetPath)> LoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", new LoginRequestDto(email, password));
        if (!response.IsSuccessStatusCode)
            return (false, "Invalid email or password.", "/login");

        var result = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        if (result is null || string.IsNullOrWhiteSpace(result.AccessToken))
            return (false, "Login response did not include an access token.", "/login");

        await _js.InvokeVoidAsync("localStorage.setItem", TokenKey, result.AccessToken);
        await _js.InvokeVoidAsync("localStorage.setItem", RefreshTokenKey, result.RefreshToken);
        return (true, null, GetLandingPath(result.AccessToken));
    }

    public async ValueTask<string?> GetTokenAsync()
        => await _js.InvokeAsync<string?>("localStorage.getItem", TokenKey);

    public async ValueTask<string> GetLandingPathAsync()
    {
        var token = await GetTokenAsync();
        return GetLandingPath(token);
    }

    public async Task<bool> RefreshTokenAsync()
    {
        var refreshToken = await _js.InvokeAsync<string?>("localStorage.getItem", RefreshTokenKey);
        if (string.IsNullOrWhiteSpace(refreshToken))
            return false;

        var response = await _http.PostAsJsonAsync("api/auth/refresh", new { RefreshToken = refreshToken });
        if (!response.IsSuccessStatusCode)
        {
            await LogoutAsync();
            return false;
        }

        var result = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        if (result is null || string.IsNullOrWhiteSpace(result.AccessToken) || string.IsNullOrWhiteSpace(result.RefreshToken))
        {
            await LogoutAsync();
            return false;
        }

        await _js.InvokeVoidAsync("localStorage.setItem", TokenKey, result.AccessToken);
        await _js.InvokeVoidAsync("localStorage.setItem", RefreshTokenKey, result.RefreshToken);
        return true;
    }

    public async Task LogoutAsync()
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", RefreshTokenKey);
    }

    private static string GetLandingPath(string? accessToken)
    {
        var roles = ReadRoles(accessToken);
        return roles.Any(IsAdminRole) ? "/admin/dashboard" : "/attendance";
    }

    private static bool IsAdminRole(string role)
        => role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
           || role.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)
           || role.Equals("HR", StringComparison.OrdinalIgnoreCase)
           || role.Equals("Manager", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<string> ReadRoles(string? accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return [];

        var parts = accessToken.Split('.');
        if (parts.Length < 2)
            return [];

        try
        {
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var document = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
            var root = document.RootElement;
            var roleKeys = new[]
            {
                "role",
                "roles",
                "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
            };

            var roles = new List<string>();
            foreach (var key in roleKeys)
            {
                if (!root.TryGetProperty(key, out var value))
                    continue;

                if (value.ValueKind == JsonValueKind.Array)
                    roles.AddRange(value.EnumerateArray().Select(x => x.GetString()).OfType<string>());
                else if (value.ValueKind == JsonValueKind.String)
                    roles.Add(value.GetString()!);
            }

            return roles;
        }
        catch
        {
            return [];
        }
    }
}
