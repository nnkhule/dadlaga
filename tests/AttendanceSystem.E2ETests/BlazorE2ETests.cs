using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Xunit;

namespace AttendanceSystem.E2ETests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class BlazorE2ECollection : ICollectionFixture<BlazorE2EHost>
{
    public const string Name = "Blazor browser tests";
}

[Collection(BlazorE2ECollection.Name)]
public sealed class BlazorE2ETests(BlazorE2EHost host)
{
    public static TheoryData<string, string> PageRoutes => new()
    {
        { "/", "Employee" },
        { "/dashboard", "Employee" },
        { "/login", "" },
        { "/forgot-password", "" },
        { "/reset-password", "" },
        { "/change-password", "Employee" },
        { "/attendance", "Employee" },
        { "/attendance/checkin", "Employee" },
        { "/leave", "Employee" },
        { "/profile", "Employee" },
        { "/statistics", "Employee" },
        { "/statistic", "Employee" },
        { "/employee/ai-chat", "Employee" },
        { "/admin/dashboard", "SuperAdmin" },
        { "/admin/attendance", "SuperAdmin" },
        { "/admin/employees", "SuperAdmin" },
        { "/admin/employees/create", "SuperAdmin" },
        { "/admin/employees/11111111-1111-1111-1111-111111111111/edit", "SuperAdmin" },
        { "/admin/departments", "SuperAdmin" },
        { "/admin/leave", "SuperAdmin" },
        { "/admin/office-locations", "SuperAdmin" },
        { "/admin/reports", "SuperAdmin" },
        { "/admin/reports/employee", "SuperAdmin" },
        { "/admin/reports/employee/11111111-1111-1111-1111-111111111111", "SuperAdmin" },
        { "/admin/ai-chat", "SuperAdmin" },
        { "/departments", "SuperAdmin" },
        { "/departments/11111111-1111-1111-1111-111111111111", "SuperAdmin" },
        { "/departments/create", "SuperAdmin" },
        { "/departments/11111111-1111-1111-1111-111111111111/edit", "SuperAdmin" },
        { "/employees", "SuperAdmin" },
        { "/employees/11111111-1111-1111-1111-111111111111", "SuperAdmin" },
        { "/notifications", "Employee" },
        { "/settings", "SuperAdmin" },
        { "/not-found", "" }
    };

    [Theory]
    [MemberData(nameof(PageRoutes))]
    public async Task Every_page_route_renders_without_a_server_error(string route, string role)
    {
        await using var browserPage = await host.NewPageAsync(string.Empty);
        var page = browserPage.Page;
        await page.SetViewportSizeAsync(375, 812);
        var response = await page.GotoAsync(host.BaseUrl + (string.IsNullOrEmpty(role) ? route : "/login"));

        Assert.NotNull(response);
        Assert.True(response!.Status < 500, $"Route {route} returned HTTP {response.Status}.");
        if (!string.IsNullOrEmpty(role))
        {
            await LogInAsync(page, role);
            if (route is not ("/" or "/dashboard") &&
                !string.Equals(new Uri(page.Url).AbsolutePath, route, StringComparison.OrdinalIgnoreCase))
            {
                await page.EvaluateAsync(
                    """route => { const link = document.createElement("a"); link.href = route; link.id = "e2e-route-link"; link.textContent = "Open route"; document.body.appendChild(link); link.click(); }""",
                    route);
            }
            else if (route is "/" or "/dashboard")
            {
                route = role == "Employee" ? "/attendance" : "/admin/dashboard";
            }

            await page.WaitForURLAsync($"**{route}");
            Assert.Equal(route, new Uri(page.Url).AbsolutePath);
        }
        await Assertions.Expect(page.Locator("body")).Not.ToContainTextAsync("An unhandled error has occurred");
        await Assertions.Expect(page.Locator("body")).Not.ToContainTextAsync("HTTP ERROR 500");
        var dimensions = await page.EvaluateAsync<int[]>(
            "() => [document.documentElement.clientWidth, document.documentElement.scrollWidth]");
        Assert.True(dimensions[1] <= dimensions[0],
            $"Route {route} overflows on a 375px viewport: document width {dimensions[1]}px.");
    }

    [Fact]
    public async Task Employee_can_check_in_and_check_out_from_attendance_page()
    {
        host.Api.ResetAttendance();
        await using var browserPage = await host.NewPageAsync("Employee");
        var page = browserPage.Page;
        await page.GotoAsync(host.BaseUrl + "/attendance");

        var checkButton = page.Locator(".check-button");
        await Assertions.Expect(checkButton).ToBeVisibleAsync();
        await Assertions.Expect(checkButton).ToContainTextAsync("CHECK IN");
        await checkButton.ClickAsync();
        await Assertions.Expect(checkButton).ToContainTextAsync("CHECK OUT");
        await checkButton.ClickAsync();
        await Assertions.Expect(checkButton).ToContainTextAsync("CHECK IN");

        Assert.Contains("POST /api/attendance/checkin", host.Api.Requests);
        Assert.Contains("POST /api/attendance/checkout", host.Api.Requests);
    }

    [Fact]
    public async Task GPS_check_in_page_validates_location_and_submits_attendance_actions()
    {
        host.Api.ResetAttendance();
        await using var browserPage = await host.NewPageAsync("Employee");
        var page = browserPage.Page;
        await page.GotoAsync(host.BaseUrl + "/attendance/checkin");

        await page.GetByRole(AriaRole.Button, new() { Name = "Validate Location" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Inside")).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Check In" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Check in recorded.")).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Check Out" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Check out recorded.")).ToBeVisibleAsync();

        Assert.Contains("GET /api/attendance/validate-location", host.Api.Requests);
        Assert.Contains("POST /api/attendance/checkin", host.Api.Requests);
        Assert.Contains("POST /api/attendance/checkout", host.Api.Requests);
    }

    [Fact]
    public async Task Employee_can_submit_a_leave_request()
    {
        host.Api.ResetLeaveRequests();
        await using var browserPage = await host.NewPageAsync("Employee");
        var page = browserPage.Page;
        await page.GotoAsync(host.BaseUrl + "/leave");

        var dates = page.Locator("input[type=date]");
        var futureDate = DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-dd");
        await dates.Nth(0).FillAsync(futureDate);
        await dates.Nth(1).FillAsync(futureDate);
        var reason = page.Locator("textarea");
        var reasonText = "Personal appointment requiring leave.";
        await reason.FillAsync(reasonText);
        await reason.PressAsync("Tab");
        await page.WaitForTimeoutAsync(500);
        var renderedLength = await page.GetByText("/ 500", new() { Exact = false }).First.InnerTextAsync();
        Assert.Contains(reasonText.Length.ToString(), renderedLength);
        var submit = page.GetByRole(AriaRole.Button, new() { Name = "Хүсэлт илгээх" });
        Assert.True(await submit.IsEnabledAsync(),
            $"Leave form remained disabled at {page.Url}. Dates: {await dates.Nth(0).InputValueAsync()}, {await dates.Nth(1).InputValueAsync()}. Reason input: {await reason.InputValueAsync()}. Requests: {string.Join(", ", host.Api.Requests)}. Browser errors: {string.Join(", ", browserPage.Errors)}. Body: {await page.Locator("body").InnerTextAsync()}");
        await submit.ClickAsync();

        await Assertions.Expect(page.GetByText("Хүсэлт амжилттай илгээгдлээ.", new() { Exact = false })).ToBeVisibleAsync();
        Assert.Contains("POST /api/leave/requests", host.Api.Requests);
    }

    [Fact]
    public async Task Administrator_can_approve_a_pending_leave_request()
    {
        host.Api.ResetLeaveRequests();
        await using var browserPage = await host.NewPageAsync(string.Empty);
        var page = browserPage.Page;
        await page.GotoAsync(host.BaseUrl + "/login");
        await LogInAsync(page, "SuperAdmin");
        await Assertions.Expect(page.GetByText("Хянах самбар")).ToBeVisibleAsync();
        await page.Locator("a[href='/admin/leave']").First.ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Чөлөөний хүсэлтүүд" })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Зөвшөөрөх" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Чөлөөний хүсэлт олдсонгүй.")).ToBeVisibleAsync();

        Assert.Contains($"POST /api/leave/requests/{MockAttendanceApi.PendingLeaveId}/approve", host.Api.Requests);
    }

    [Fact]
    public async Task Administrator_can_reject_a_pending_leave_request_with_a_reason()
    {
        host.Api.ResetLeaveRequests();
        await using var browserPage = await host.NewPageAsync(string.Empty);
        var page = browserPage.Page;
        await page.GotoAsync(host.BaseUrl + "/login");
        await LogInAsync(page, "SuperAdmin");
        await page.Locator("a[href='/admin/leave']").First.ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Чөлөөний хүсэлтүүд" })).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Татгалзах" }).First.ClickAsync();
        await page.GetByPlaceholder("Шалтгаан бичнэ үү...").FillAsync("Please provide supporting details.");
        await page.GetByRole(AriaRole.Button, new() { Name = "Татгалзах" }).Last.ClickAsync();
        await Assertions.Expect(page.GetByText("Чөлөөний хүсэлт олдсонгүй.")).ToBeVisibleAsync();

        Assert.Contains($"POST /api/leave/requests/{MockAttendanceApi.PendingLeaveId}/reject", host.Api.Requests);
    }

    [Fact]
    public async Task Employee_can_cancel_a_pending_leave_request()
    {
        host.Api.ResetLeaveRequests(withPendingEmployeeRequest: true);
        await using var browserPage = await host.NewPageAsync("Employee");
        var page = browserPage.Page;
        await page.GotoAsync(host.BaseUrl + "/leave");

        await page.GetByRole(AriaRole.Button, new() { Name = "Хүсэлт цуцлах" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Хүсэлт байхгүй байна.")).ToBeVisibleAsync();

        Assert.Contains($"POST /api/leave/requests/{MockAttendanceApi.PendingLeaveId}/cancel", host.Api.Requests);
    }

    [Fact]
    public async Task Notification_bell_is_available_for_employee_and_admin_and_can_mark_read()
    {
        foreach (var role in new[] { "Employee", "SuperAdmin" })
        {
            host.Api.ResetNotifications();
            await using var browserPage = await host.NewPageAsync(role == "Employee" ? role : string.Empty);
            var page = browserPage.Page;
            if (role == "Employee")
            {
                await page.GotoAsync(host.BaseUrl + "/attendance");
            }
            else
            {
                await page.GotoAsync(host.BaseUrl + "/login");
                await LogInAsync(page, role);
            }

            await Assertions.Expect(page.Locator(role == "Employee" ? ".employee-shell" : ".admin-shell")).ToBeVisibleAsync();
            await Assertions.Expect(page.Locator(".notification")).ToBeVisibleAsync();
            Assert.Contains("GET /api/notifications/unread-count", host.Api.Requests);
            await Assertions.Expect(page.Locator(".notification-count")).ToHaveTextAsync("1");
            await page.GetByRole(AriaRole.Button, new() { Name = "Мэдэгдэл" }).ClickAsync();
            await Assertions.Expect(page.GetByText("Чөлөөний хүсэлт шинэчлэгдлээ")).ToBeVisibleAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Mark read" }).ClickAsync();
            await Assertions.Expect(page.Locator(".notification-count")).ToHaveCountAsync(0);

            Assert.Contains($"POST /api/notifications/{MockAttendanceApi.NotificationId}/read", host.Api.Requests);
        }
    }

    [Fact]
    public async Task Employee_leave_request_requires_an_attachment_for_sick_leave()
    {
        await using var browserPage = await host.NewPageAsync("Employee");
        var page = browserPage.Page;
        await page.GotoAsync(host.BaseUrl + "/leave");

        var dates = page.Locator("input[type=date]");
        var futureDate = DateTime.UtcNow.AddDays(10).ToString("yyyy-MM-dd");
        await dates.Nth(0).FillAsync(futureDate);
        await dates.Nth(1).FillAsync(futureDate);
        await page.Locator("textarea").FillAsync("A sufficiently detailed reason.");
        await page.Locator("textarea").PressAsync("Tab");
        var submit = page.GetByRole(AriaRole.Button, new() { Name = "Хүсэлт илгээх" });
        await Assertions.Expect(submit).ToBeEnabledAsync();

        await page.Locator("select").SelectOptionAsync("Sick");
        await Assertions.Expect(page.GetByText("Баримт бичиг")).ToBeVisibleAsync();
        await Assertions.Expect(submit).ToBeDisabledAsync();
        Assert.DoesNotContain("POST /api/leave/requests", host.Api.Requests);
    }

    [Fact]
    public async Task Employee_hourly_leave_request_rejects_duration_over_eight_hours()
    {
        await using var browserPage = await host.NewPageAsync("Employee");
        var page = browserPage.Page;
        await page.GotoAsync(host.BaseUrl + "/leave");
        await page.GetByRole(AriaRole.Button, new() { Name = "⏰ Цаг" }).ClickAsync();
        await page.Locator("input[type=time]").Nth(0).FillAsync("08:00");
        await page.Locator("input[type=time]").Nth(1).FillAsync("17:00");
        await page.Locator("textarea").FillAsync("A sufficiently detailed reason.");
        await page.Locator("textarea").PressAsync("Tab");

        await Assertions.Expect(page.GetByText("1 өдрөөс илүү хэмжээтэй байна")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Хүсэлт илгээх" })).ToBeDisabledAsync();
        Assert.DoesNotContain("POST /api/leave/requests", host.Api.Requests);
    }

    [Fact]
    public async Task Employee_and_admin_leave_pages_fit_mobile_and_tablet_viewports()
    {
        foreach (var (role, route) in new[] { ("Employee", "/leave"), ("SuperAdmin", "/admin/leave") })
        {
            await using var browserPage = await host.NewPageAsync(string.Empty);
            var page = browserPage.Page;
            await page.GotoAsync(host.BaseUrl + "/login");
            await LogInAsync(page, role);
            await page.GotoAsync(host.BaseUrl + route);

            foreach (var width in new[] { 375, 768, 1280 })
            {
                await page.SetViewportSizeAsync(width, 900);
                var dimensions = await page.EvaluateAsync<int[]>(
                    "() => [document.documentElement.clientWidth, document.documentElement.scrollWidth]");
                Assert.True(dimensions[1] <= dimensions[0],
                    $"{route} overflows at {width}px: viewport {dimensions[0]}px, document {dimensions[1]}px.");
            }
        }
    }

    [Fact]
    public async Task Login_form_submits_credentials_and_navigates_to_employee_home()
    {
        await using var browserPage = await host.NewPageAsync(string.Empty);
        var page = browserPage.Page;
        await page.GotoAsync(host.BaseUrl + "/login");
        await page.GetByLabel("Email Address").First.FillAsync("employee@example.test");
        await page.GetByLabel("Password").FillAsync("ValidPassword123!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();

        await page.WaitForURLAsync("**/attendance");
        Assert.Contains("POST /api/auth/login", host.Api.Requests);
    }

    private static async Task LogInAsync(IPage page, string role)
    {
        var email = role == "Employee" ? "employee@example.test" : "admin@example.test";
        await page.GetByLabel("Email Address").First.FillAsync(email);
        await page.GetByLabel("Password").FillAsync("ValidPassword123!");
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign In" }).ClickAsync();
        var landingPath = role == "Employee" ? "/attendance" : "/admin/dashboard";
        await page.WaitForURLAsync($"**{landingPath}", new PageWaitForURLOptions
        {
            WaitUntil = WaitUntilState.DOMContentLoaded
        });
        if (role == "Employee")
            await Assertions.Expect(page.Locator(".employee-shell")).ToBeVisibleAsync();
        else
            await Assertions.Expect(page.Locator(".admin-shell")).ToBeVisibleAsync();
    }
}

public sealed class BlazorE2EHost : IAsyncLifetime
{
    private readonly string _root = FindRepositoryRoot();
    private readonly int _webPort = GetFreePort();
    private readonly ConcurrentQueue<string> _hostOutput = new();
    private Process? _webProcess;
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public MockAttendanceApi Api { get; } = new();
    public string BaseUrl => $"http://127.0.0.1:{_webPort}";

    public async Task InitializeAsync()
    {
        await Api.StartAsync();
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });

        var project = Path.Combine(_root, "src", "AttendanceSystem.Blazor", "AttendanceSystem.Blazor.csproj");
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("run");
        start.ArgumentList.Add("--no-build");
        start.ArgumentList.Add("--no-launch-profile");
        start.ArgumentList.Add("--project");
        start.ArgumentList.Add(project);
        start.ArgumentList.Add("--");
        start.ArgumentList.Add("--urls");
        start.ArgumentList.Add(BaseUrl);
        start.ArgumentList.Add($"--ApiBaseUrl={Api.BaseUrl}");
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";

        _webProcess = Process.Start(start) ?? throw new InvalidOperationException("Could not start the Blazor test host.");
        _webProcess.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
                _hostOutput.Enqueue(eventArgs.Data);
        };
        _webProcess.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
                _hostOutput.Enqueue(eventArgs.Data);
        };
        _webProcess.BeginOutputReadLine();
        _webProcess.BeginErrorReadLine();

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            if (_webProcess.HasExited)
                throw new InvalidOperationException($"Blazor test host exited with code {_webProcess.ExitCode}.");

            try
            {
                using var response = await client.GetAsync(BaseUrl + "/login");
                if ((int)response.StatusCode < 500)
                    return;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                await Task.Delay(250);
            }
        }

        throw new TimeoutException($"Blazor test host did not start within 45 seconds.{Environment.NewLine}{string.Join(Environment.NewLine, _hostOutput)}");
    }

    public async Task<PageLease> NewPageAsync(string role)
    {
        var context = await _browser!.NewContextAsync(new BrowserNewContextOptions
        {
            Permissions = ["geolocation"],
            Geolocation = new Geolocation { Latitude = 47.9123f, Longitude = 106.9103f }
        });
        if (!string.IsNullOrWhiteSpace(role))
            await context.AddInitScriptAsync($"localStorage.setItem('attendance.accessToken', '{CreateJwt(role)}');");
        var page = await context.NewPageAsync();
        var errors = new ConcurrentQueue<string>();
        page.Console += (_, message) =>
        {
            if (message.Type == "error")
                errors.Enqueue(message.Text);
        };
        page.PageError += (_, error) => errors.Enqueue(error);
        return new PageLease(context, page, errors);
    }

    public async Task DisposeAsync()
    {
        if (_webProcess is { HasExited: false })
        {
            _webProcess.Kill(entireProcessTree: true);
            await _webProcess.WaitForExitAsync();
        }

        _webProcess?.Dispose();
        if (_browser is not null)
            await _browser.DisposeAsync();
        _playwright?.Dispose();
        await Api.DisposeAsync();
    }

    private static string CreateJwt(string role)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            sub = "11111111-1111-1111-1111-111111111111",
            name = "Test Employee",
            email = "employee@example.test",
            employee_id = "11111111-1111-1111-1111-111111111111",
            role,
            exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
        });
        return $"e30.{Convert.ToBase64String(payload).TrimEnd('=').Replace('+', '-').Replace('/', '_')}.test";
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AttendanceSystem.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not find AttendanceSystem.sln.");
    }

    private static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}

public sealed class PageLease(IBrowserContext context, IPage page, ConcurrentQueue<string> errors) : IAsyncDisposable
{
    public IPage Page { get; } = page;
    public IReadOnlyCollection<string> Errors => errors.ToArray();

    public async ValueTask DisposeAsync()
        => await context.DisposeAsync();
}

public sealed class MockAttendanceApi : IAsyncDisposable
{
    public static readonly Guid PendingLeaveId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid NotificationId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private readonly ConcurrentQueue<string> _requests = new();
    private WebApplication? _application;
    private bool _checkedIn;
    private bool _checkedOut;
    private bool _leaveApproved;
    private bool _leaveSubmitted;
    private bool _leaveCancelled;
    private bool _notificationRead;

    public string BaseUrl { get; private set; } = string.Empty;
    public IReadOnlyCollection<string> Requests => _requests.ToArray();

    public async Task StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _application = builder.Build();
        _application.Use(async (context, next) =>
        {
            try
            {
                await next();
                _requests.Enqueue($"DONE {context.Request.Method} {context.Request.Path} {context.Response.StatusCode}");
            }
            catch (Exception exception)
            {
                _requests.Enqueue($"ERROR {context.Request.Method} {context.Request.Path}: {exception}");
                throw;
            }
        });
        _application.MapFallback(HandleAsync);
        await _application.StartAsync();
        BaseUrl = _application.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.Single();
    }

    public void ResetAttendance()
    {
        _checkedIn = false;
        _checkedOut = false;
        ClearRequests();
    }

    public void ResetLeaveRequests(bool withPendingEmployeeRequest = false)
    {
        _leaveApproved = false;
        _leaveSubmitted = withPendingEmployeeRequest;
        _leaveCancelled = false;
        ClearRequests();
    }

    public void ResetNotifications()
    {
        _notificationRead = false;
        ClearRequests();
    }

    public async ValueTask DisposeAsync()
    {
        if (_application is not null)
        {
            await _application.StopAsync();
            await _application.DisposeAsync();
        }
    }

    private async Task HandleAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.TrimStart('/') ?? string.Empty;
        var method = context.Request.Method.ToUpperInvariant();
        _requests.Enqueue($"{method} /{path}");
        context.Response.ContentType = "application/json";

        if (path == "api/auth/login" && method == "POST")
        {
            using var login = await JsonDocument.ParseAsync(context.Request.Body);
            var email = login.RootElement.TryGetProperty("email", out var emailElement)
                ? emailElement.GetString()
                : null;
            var role = email?.Equals("admin@example.test", StringComparison.OrdinalIgnoreCase) == true
                ? "SuperAdmin"
                : "Employee";
            await JsonAsync(context, new
            {
                accessToken = CreateJwt(role),
                refreshToken = "test-refresh-token",
                expiresAt = DateTime.UtcNow.AddHours(1),
                employeeId = "11111111-1111-1111-1111-111111111111"
            });
            return;
        }

        if (path == "api/employees/me")
        {
            await JsonAsync(context, new
            {
                id = "11111111-1111-1111-1111-111111111111",
                employeeCode = "TEST001",
                fullName = "Test Employee",
                email = "employee@example.test",
                department = "Test Department",
                departmentName = "Test Department",
                hireDate = "2020-01-01",
                isActive = true
            });
            return;
        }

        if (path == "api/settings/office-locations")
        {
            await JsonAsync(context, new[]
            {
                new { id = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", name = "Test Office", latitude = 47.9123, longitude = 106.9103, radiusMeters = 500, isActive = true }
            });
            return;
        }

        if (path == "api/attendance/today")
        {
            if (!_checkedIn)
            {
                await JsonAsync(context, null);
                return;
            }
            await JsonAsync(context, new
            {
                id = "cccccccc-cccc-cccc-cccc-cccccccccccc",
                employeeId = "11111111-1111-1111-1111-111111111111",
                date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                checkInTime = "2026-10-02T09:00:00",
                checkOutTime = _checkedOut ? "2026-10-02T17:00:00" : null,
                workHours = _checkedOut ? 8 : 0,
                overtime = 0,
                overtimeHours = 0,
                shortHours = 0,
                lateMinutes = 0,
                verificationMethod = "Gps",
                attendanceStatus = "Present",
                status = "Present",
                isSuspicious = false,
                isAutoGeo = false
            });
            return;
        }

        if (path == "api/attendance/checkin" && method == "POST")
        {
            _checkedIn = true;
            await JsonAsync(context, new { success = true });
            return;
        }

        if (path == "api/attendance/checkout" && method == "POST")
        {
            _checkedOut = true;
            await JsonAsync(context, new { success = true });
            return;
        }

        if (path == "api/attendance/validate-location")
        {
            await JsonAsync(context, new
            {
                currentLocation = "47.912300, 106.910300",
                officeLocation = "Test Office",
                distance = 0,
                distanceMeters = 0,
                validationStatus = "Inside",
                isValid = true,
                isWithinAllowedRadius = true
            });
            return;
        }

        if (path == "api/leave/statistics")
        {
            await JsonAsync(context, new { allocated = 15, used = 0, remaining = 15, pendingRequests = 0 });
            return;
        }

        if (path == "api/notifications/unread-count")
        {
            await JsonAsync(context, new { count = _notificationRead ? 0 : 1 });
            return;
        }

        if (path == "api/notifications" && method == "GET")
        {
            var items = _notificationRead
                ? Array.Empty<object>()
                : new[] { new { id = NotificationId, title = "Чөлөөний хүсэлт шинэчлэгдлээ", message = "Таны хүсэлтийг хүлээн авлаа.", createdDate = DateTime.UtcNow, createdAt = DateTime.UtcNow, isRead = false } };
            await JsonAsync(context, new { items, pageNumber = 1, pageSize = 5, totalCount = items.Length });
            return;
        }

        if (path == $"api/notifications/{NotificationId}/read" && method == "POST")
        {
            _notificationRead = true;
            await JsonAsync(context, new { message = "Notification marked as read." });
            return;
        }

        if (path == "api/leave/calendar")
        {
            await JsonAsync(context, new { approvedLeaves = Array.Empty<object>(), holidays = Array.Empty<object>() });
            return;
        }

        if (path == "api/leave/team-calendar")
        {
            await JsonAsync(context, Array.Empty<object>());
            return;
        }

        if (path == "api/leave/history")
        {
            var items = _leaveSubmitted && !_leaveCancelled
                ? new[] { new { id = PendingLeaveId, employeeId = "11111111-1111-1111-1111-111111111111", employeeName = "Test Employee", startDate = "2026-10-12", endDate = "2026-10-12", leaveType = "Annual", reason = "Personal appointment requiring leave.", approvalStatus = "Pending", status = "Pending", totalDays = 1, leaveMode = "Daily" } }
                : Array.Empty<object>();
            await JsonAsync(context, new { items, pageNumber = 1, pageSize = 20, totalCount = items.Length });
            return;
        }

        if (path == "api/leave/requests" && method == "GET")
        {
            var items = !_leaveApproved
                ? new[] { new { id = PendingLeaveId, employeeId = "11111111-1111-1111-1111-111111111111", employeeName = "Test Employee", startDate = "2026-10-12", endDate = "2026-10-12", leaveType = "Annual", reason = "Test leave request.", approvalStatus = "Pending", status = "Pending", totalDays = 1, leaveMode = "Daily" } }
                : Array.Empty<object>();
            await JsonAsync(context, new { items, pageNumber = 1, pageSize = 100, totalCount = items.Length });
            return;
        }

        if (path == "api/leave/requests" && method == "POST")
        {
            _leaveSubmitted = true;
            context.Response.StatusCode = StatusCodes.Status201Created;
            await JsonAsync(context, new { message = "Leave request received" });
            return;
        }

        if (path == $"api/leave/requests/{PendingLeaveId}/approve" && method == "POST")
        {
            _leaveApproved = true;
            await JsonAsync(context, new { success = true });
            return;
        }

        if (path == $"api/leave/requests/{PendingLeaveId}/reject" && method == "POST")
        {
            _leaveApproved = true;
            await JsonAsync(context, new { success = true });
            return;
        }

        if (path == $"api/leave/requests/{PendingLeaveId}/cancel" && method == "POST")
        {
            _leaveCancelled = true;
            await JsonAsync(context, new { success = true });
            return;
        }

        if (path.StartsWith("api/", StringComparison.Ordinal))
        {
            if (method == "POST" || method == "PUT" || method == "DELETE")
                await JsonAsync(context, new { success = true });
            else if (path.Contains("/notifications", StringComparison.Ordinal) ||
                     path.Contains("office-locations", StringComparison.Ordinal))
                await JsonAsync(context, Array.Empty<object>());
            else
                await JsonAsync(context, new { items = Array.Empty<object>(), pageNumber = 1, pageSize = 20, totalCount = 0 });
            return;
        }

        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await JsonAsync(context, new { error = "Not found" });
    }

    private void ClearRequests()
    {
        while (_requests.TryDequeue(out _)) { }
    }

    private static Task JsonAsync(HttpContext context, object? value)
        => context.Response.WriteAsJsonAsync(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static string CreateJwt(string role)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            sub = "11111111-1111-1111-1111-111111111111",
            name = "Test Employee",
            email = "employee@example.test",
            employee_id = "11111111-1111-1111-1111-111111111111",
            role,
            roles = role == "SuperAdmin"
                ? new[] { "SuperAdmin", "HRManager", "DepartmentHead" }
                : new[] { role },
            exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
        });
        return $"e30.{Convert.ToBase64String(payload).TrimEnd('=').Replace('+', '-').Replace('/', '_')}.test";
    }
}
