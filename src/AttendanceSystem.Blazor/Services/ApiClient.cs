using System.Net.Http.Headers;
using System.Net.Http.Json;
using AttendanceSystem.Blazor.Models;

namespace AttendanceSystem.Blazor.Services;

public sealed class ApiClient
{
    private readonly HttpClient _http;
    private readonly AuthService _auth;

    public ApiClient(HttpClient http, AuthService auth)
    {
        _http = http;
        _auth = auth;
    }

    public async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync();
        return await _http.GetFromJsonAsync<T>(url, cancellationToken);
    }

    public async Task<HttpResponseMessage> PostAsync<T>(string url, T body, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync();
        return await _http.PostAsJsonAsync(url, body, cancellationToken);
    }

    public async Task<HttpResponseMessage> PutAsync<T>(string url, T body, CancellationToken cancellationToken = default)
    {
        await AuthorizeAsync();
        return await _http.PutAsJsonAsync(url, body, cancellationToken);
    }

    public Task<DashboardSummaryDto?> GetDashboardSummaryAsync()
        => GetAsync<DashboardSummaryDto>("api/v1/dashboard/summary");

    public async Task<IReadOnlyList<RecentActivityDto>?> GetRecentActivitiesAsync()
    {
        var response = await GetAsync<PagedResponse<RecentActivityResponse>>("api/v1/dashboard/recent-activities?pageNumber=1&pageSize=10");
        return response?.Items
            .Select(x => new RecentActivityDto(x.Id, x.Type, x.Title, x.Description ?? string.Empty, x.CreatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<AttendanceTrendDto>?> GetAttendanceTrendsAsync()
    {
        var response = await GetAsync<AttendanceTrendResponse>("api/v1/dashboard/attendance-trends");
        if (response is null)
            return [];

        var count = new[] { response.Labels.Count, response.PresentCounts.Count, response.AbsentCounts.Count, response.LateCounts.Count }.Min();
        var items = new List<AttendanceTrendDto>();
        for (var i = 0; i < count; i++)
        {
            var date = DateOnly.TryParse(response.Labels[i], out var parsed)
                ? parsed
                : DateOnly.FromDateTime(DateTime.Today.AddDays(i - count + 1));

            items.Add(new AttendanceTrendDto(date, response.PresentCounts[i], response.AbsentCounts[i], response.LateCounts[i]));
        }

        return items;
    }

    private async Task AuthorizeAsync()
    {
        var token = await _auth.GetTokenAsync();
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    private sealed record RecentActivityResponse(Guid Id, string Type, string Title, string? Description, DateTime CreatedAt);
    private sealed record AttendanceTrendResponse(IReadOnlyList<string> Labels, IReadOnlyList<int> PresentCounts, IReadOnlyList<int> AbsentCounts, IReadOnlyList<int> LateCounts);
}
