using System.Net.Http.Json;
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

    public async Task<(bool Success, string? Error)> LoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", new LoginRequestDto(email, password));
        if (!response.IsSuccessStatusCode)
            return (false, "Invalid email or password.");

        var result = await response.Content.ReadFromJsonAsync<LoginResponseDto>();
        if (result is null || string.IsNullOrWhiteSpace(result.AccessToken))
            return (false, "Login response did not include an access token.");

        await _js.InvokeVoidAsync("localStorage.setItem", TokenKey, result.AccessToken);
        await _js.InvokeVoidAsync("localStorage.setItem", RefreshTokenKey, result.RefreshToken);
        return (true, null);
    }

    public async ValueTask<string?> GetTokenAsync()
        => await _js.InvokeAsync<string?>("localStorage.getItem", TokenKey);

    public async Task LogoutAsync()
    {
        await _js.InvokeVoidAsync("localStorage.removeItem", TokenKey);
        await _js.InvokeVoidAsync("localStorage.removeItem", RefreshTokenKey);
    }
}
