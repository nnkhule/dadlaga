using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace AttendanceSystem.Blazor.Services;

/// <summary>
/// Provides authentication state for Blazor Server components.
/// This provider allows components to access the current authentication state.
/// </summary>
public class PersistingAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PersistingAuthenticationStateProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        
        if (httpContext?.User?.Identity?.IsAuthenticated ?? false)
        {
            // User is authenticated
            return Task.FromResult(new AuthenticationState(httpContext.User));
        }

        // Return anonymous user
        var anonymousPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        return Task.FromResult(new AuthenticationState(anonymousPrincipal));
    }
}
