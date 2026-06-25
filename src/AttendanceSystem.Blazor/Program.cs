using AttendanceSystem.Blazor.Components;
using AttendanceSystem.Blazor.Services;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add Razor components with interactive server rendering
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ── Auth ──────────────────────────────────────────────────────────────────────
// The original code registered BOTH AddAuthentication(CookieAuth) AND a JWT-based
// PersistingAuthenticationStateProvider — they fought each other.
// The Blazor app uses only JWT tokens stored in localStorage, so we only need
// AddAuthorizationCore + the custom state provider; no cookie middleware needed.
builder.Services.AddAuthorizationCore();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthorization();

builder.Services.AddScoped<PersistingAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    sp => sp.GetRequiredService<PersistingAuthenticationStateProvider>());

// ── HTTP client pointing at the API ──────────────────────────────────────────
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7000/")
});

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ApiClient>();

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// UseStatusCodePagesWithReExecute does NOT accept a createScopeForStatusCodePages parameter.
// Passing an unknown overload caused a compile error / startup crash that looked like
// an infinite loading screen because Blazor never fully started.
app.UseStatusCodePagesWithReExecute("/not-found");

if (!app.Environment.IsDevelopment()
    || builder.Configuration["https_port"] is not null
    || builder.Configuration["HTTPS_PORT"] is not null)
{
    app.UseHttpsRedirection();
}

// Authorization middleware (no cookie authentication needed — JWT only)

app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();