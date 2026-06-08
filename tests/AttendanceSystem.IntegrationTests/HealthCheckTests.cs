using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AttendanceSystem.IntegrationTests;

/// <summary>
/// Basic API host integration tests (Phase 2 will add Testcontainers SQL).
/// </summary>
public class HealthCheckTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public HealthCheckTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact(Skip = "Requires SQL Server; enable with Testcontainers in Phase 2")]
    public async Task Swagger_IsAvailable()
    {
        var response = await _client.GetAsync("http://localhost:5000/swagger/index.html");
        response.IsSuccessStatusCode.Should().BeTrue();
    }
}
