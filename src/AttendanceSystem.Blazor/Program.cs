using AttendanceSystem.Blazor.Components;
using AttendanceSystem.Blazor.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Razor Components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Authentication & Authorization
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/access-denied";
    });

builder.Services.AddAuthorizationCore();
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddScoped<PersistingAuthenticationStateProvider>();

builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<PersistingAuthenticationStateProvider>());

// HttpClient
builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(
        builder.Configuration["ApiBaseUrl"]
        ?? "https://localhost:7000/")
});

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ApiClient>();

var app = builder.Build();

// Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found");

if (!app.Environment.IsDevelopment()
    || builder.Configuration["https_port"] is not null
    || builder.Configuration["HTTPS_PORT"] is not null)
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();