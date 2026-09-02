
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

    // Prerender үед anonymous state буцаана — JS ажилладаггүй тул
    private static readonly Task<AuthenticationState> _anonymous =
        Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));

    public PersistingAuthenticationStateProvider(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            // Prerender (SSR) үед IJSRuntime ажилладаггүй — anonymous буцаана
            if (_jsRuntime is IJSInProcessRuntime)
            {
                // WebAssembly — шууд ажиллана
            }
            else
            {
                // Server-side: prerender болон interactive хоёуланд ажиллана
                // Харин prerender үед InvokeAsync exception шидэж болно
            }

            string? token;
            try
            {
                token = await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", TokenKey);
            }
            catch (InvalidOperationException)
            {
                // Prerender үед JS interop боломжгүй — anonymous буцаана
                return await _anonymous;
            }
            catch (JSException)
            {
                return await _anonymous;
            }

            if (string.IsNullOrWhiteSpace(token))
                return await _anonymous;

            // Token хугацаа дууссан эсэх шалгах
            if (IsTokenExpired(token))
                return await _anonymous;

            var identity = BuildClaimsIdentity(token);
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch
        {
            return await _anonymous;
        }
    }

    public Task NotifyUserAuthenticationAsync()
    {
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        return Task.CompletedTask;
    }

    public void NotifyUserLogout()
    {
        NotifyAuthenticationStateChanged(_anonymous);
    }

    private static bool IsTokenExpired(string jwt)
    {
        var payload = GetPayload(jwt);
        if (payload is null) return true;

        if (payload.Value.TryGetProperty("exp", out var exp) && exp.ValueKind == JsonValueKind.Number)
        {
            var expiry = DateTimeOffset.FromUnixTimeSeconds(exp.GetInt64());
            return expiry < DateTimeOffset.UtcNow;
        }
        return false;
    }

    private static ClaimsIdentity BuildClaimsIdentity(string accessToken)
    {
        var identity = new ClaimsIdentity("jwt");
        var payload  = GetPayload(accessToken);
        if (payload is null) return identity;

        var p = payload.Value;

        void Add(string type, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                identity.AddClaim(new Claim(type, value!));
        }

        if (p.TryGetProperty("sub",   out var sub)   && sub.ValueKind   == JsonValueKind.String) Add(ClaimTypes.NameIdentifier, sub.GetString());
        if (p.TryGetProperty("name",  out var name)  && name.ValueKind  == JsonValueKind.String) Add(ClaimTypes.Name,           name.GetString());
        if (p.TryGetProperty("email", out var email) && email.ValueKind == JsonValueKind.String) Add(ClaimTypes.Email,          email.GetString());
        if (p.TryGetProperty("employee_id", out var employeeId) && employeeId.ValueKind == JsonValueKind.String) Add("employee_id", employeeId.GetString());
        if (p.TryGetProperty("department_id", out var departmentId) && departmentId.ValueKind == JsonValueKind.String) Add("department_id", departmentId.GetString());

        // Role claim-ууд — ASP.NET Identity-н урт нэр болон богино нэр хоёуланг дэмжих
        foreach (var key in new[]
        {
            "role",
            "roles",
            "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
        })
        {
            if (!p.TryGetProperty(key, out var roleVal)) continue;

            if (roleVal.ValueKind == JsonValueKind.String)
                Add(ClaimTypes.Role, roleVal.GetString());
            else if (roleVal.ValueKind == JsonValueKind.Array)
                foreach (var item in roleVal.EnumerateArray())
                    if (item.ValueKind == JsonValueKind.String)
                        Add(ClaimTypes.Role, item.GetString());
        }

        // Name fallback
        if (!identity.HasClaim(c => c.Type == ClaimTypes.Name))
            Add(ClaimTypes.Name,
                identity.FindFirst(ClaimTypes.Email)?.Value
                ?? identity.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        return identity;
    }

    private static JsonElement? GetPayload(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2) return null;

        var b64 = parts[1].Replace('-', '+').Replace('_', '/');
        b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');

        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(b64));
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch { return null; }
    }
}