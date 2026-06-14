using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace AttendanceSystem.Blazor.Services;

public class PersistingAuthenticationStateProvider : AuthenticationStateProvider
{
    private const string TokenKey = "attendance.accessToken";
    private readonly IJSRuntime _jsRuntime;

    public PersistingAuthenticationStateProvider(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var token = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", TokenKey);
            if (string.IsNullOrWhiteSpace(token))
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

            var identity = BuildClaimsIdentity(token);
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    public Task NotifyUserAuthenticationAsync()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        return Task.CompletedTask;
    }

    public void NotifyUserLogout()
    {
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()))));
    }

    private static ClaimsIdentity BuildClaimsIdentity(string accessToken)
    {
        var identity = new ClaimsIdentity("jwt");
        var payload = GetPayload(accessToken);
        if (!payload.HasValue)
            return identity;

        var payloadElement = payload.Value;

        void AddClaim(string claimType, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                identity.AddClaim(new Claim(claimType, value!));
        }

        if (payloadElement.TryGetProperty("sub", out var sub) && sub.ValueKind == JsonValueKind.String)
            AddClaim(ClaimTypes.NameIdentifier, sub.GetString());

        if (payloadElement.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
            AddClaim(ClaimTypes.Name, name.GetString());

        if (payloadElement.TryGetProperty("email", out var email) && email.ValueKind == JsonValueKind.String)
            AddClaim(ClaimTypes.Email, email.GetString());

        var roleProperties = new[]
        {
            "role",
            "roles",
            "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
        };

        foreach (var roleProperty in roleProperties)
        {
            if (!payloadElement.TryGetProperty(roleProperty, out var roleValue))
                continue;

            if (roleValue.ValueKind == JsonValueKind.String)
            {
                AddClaim(ClaimTypes.Role, roleValue.GetString());
            }
            else if (roleValue.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in roleValue.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        AddClaim(ClaimTypes.Role, item.GetString());
                }
            }
        }

        if (!identity.HasClaim(c => c.Type == ClaimTypes.Name) && identity.HasClaim(c => c.Type == ClaimTypes.Email))
            AddClaim(ClaimTypes.Name, identity.FindFirst(ClaimTypes.Email)?.Value);

        if (!identity.HasClaim(c => c.Type == ClaimTypes.Name) && identity.HasClaim(c => c.Type == ClaimTypes.NameIdentifier))
            AddClaim(ClaimTypes.Name, identity.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        return identity;
    }

    private static JsonElement? GetPayload(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2)
            return null;

        var payload = parts[1]
            .Replace('-', '+')
            .Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }
}
