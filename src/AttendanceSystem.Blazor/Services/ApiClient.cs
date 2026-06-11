using System.Net.Http.Headers;
using System.Net.Http.Json;
using AttendanceSystem.Blazor.Models;
using Microsoft.AspNetCore.Components;

namespace AttendanceSystem.Blazor.Services;

public sealed class ApiClient
{
    private readonly HttpClient _http;
    private readonly AuthService _auth;
    private readonly NavigationManager _navigation;

    public ApiClient(HttpClient http, AuthService auth, NavigationManager navigation)
    {
        _http = http;
        _auth = auth;
        _navigation = navigation;
    }

    public async Task<T?> GetAsync<T>(string url, CancellationToken cancellationToken = default)
    {
        var response = await SendWithRefreshAsync(() => _http.GetAsync(url, cancellationToken));
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    public async Task<HttpResponseMessage> PostAsync<T>(string url, T body, CancellationToken cancellationToken = default)
    {
        return await SendWithRefreshAsync(() => _http.PostAsJsonAsync(url, body, cancellationToken));
    }

    public async Task<HttpResponseMessage> PutAsync<T>(string url, T body, CancellationToken cancellationToken = default)
    {
        return await SendWithRefreshAsync(() => _http.PutAsJsonAsync(url, body, cancellationToken));
    }

    public Task<DashboardSummaryDto?> GetDashboardSummaryAsync()
        => GetAsync<DashboardSummaryDto>("api/dashboard/summary");

    public async Task<IReadOnlyList<RecentActivityDto>?> GetRecentActivitiesAsync()
    {
        var response = await GetAsync<PagedResponse<RecentActivityResponse>>("api/dashboard/recent-activities?pageNumber=1&pageSize=10");
        return response?.Items
            .Select(x => new RecentActivityDto(x.Id, x.Type, x.Title, x.Description ?? string.Empty, x.CreatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<AttendanceTrendDto>?> GetAttendanceTrendsAsync()
    {
        var response = await GetAsync<AttendanceTrendResponse>("api/dashboard/statistics");
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

    public Task<AttendanceDto?> GetTodayAttendanceAsync()
        => GetAsync<AttendanceDto>("api/attendance/today");

    public Task<AttendanceStatisticsDto?> GetMyAttendanceStatisticsAsync(DateOnly? from = null, DateOnly? to = null)
    {
        var query = BuildDateQuery(from, to);
        return GetAsync<AttendanceStatisticsDto>($"api/attendance/statistics{query}");
    }

    public async Task<IReadOnlyList<AttendanceDto>> GetMyAttendanceHistoryAsync(DateOnly? from = null, DateOnly? to = null, int pageSize = 7)
    {
        var query = BuildDateQuery(from, to, $"pageSize={pageSize}");
        var response = await GetAsync<PagedResponse<AttendanceDto>>($"api/attendance/history{query}");
        return response?.Items ?? [];
    }

    public Task<HttpResponseMessage> CheckInAsync(AttendanceActionRequest request)
        => PostAsync("api/attendance/checkin", request);

    public Task<HttpResponseMessage> CheckOutAsync(AttendanceActionRequest request)
        => PostAsync("api/attendance/checkout", request);

    private async Task AuthorizeAsync()
    {
        var token = await _auth.GetTokenAsync();
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(token)
            ? null
            : new AuthenticationHeaderValue("Bearer", token);
    }

    private static string BuildDateQuery(DateOnly? from, DateOnly? to, string? extra = null)
    {
        var values = new List<string>();
        if (from.HasValue) values.Add($"from={from:yyyy-MM-dd}");
        if (to.HasValue) values.Add($"to={to:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(extra)) values.Add(extra);
        return values.Count == 0 ? string.Empty : "?" + string.Join("&", values);
    }

    private async Task<HttpResponseMessage> SendWithRefreshAsync(Func<Task<HttpResponseMessage>> send)
    {
        await AuthorizeAsync();
        var response = await send();
        if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
        {
            response.EnsureSuccessStatusCode();
            return response;
        }

        response.Dispose();
        if (await _auth.RefreshTokenAsync())
        {
            await AuthorizeAsync();
            response = await send();
            if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            {
                response.EnsureSuccessStatusCode();
                return response;
            }

            response.Dispose();
        }

        await _auth.LogoutAsync();
        _navigation.NavigateTo("/login", forceLoad: true);
        throw new UnauthorizedAccessException("Your session has expired. Please sign in again.");
    }

    private sealed record RecentActivityResponse(Guid Id, string Type, string Title, string? Description, DateTime CreatedAt);
    private sealed record AttendanceTrendResponse(IReadOnlyList<string> Labels, IReadOnlyList<int> PresentCounts, IReadOnlyList<int> AbsentCounts, IReadOnlyList<int> LateCounts);
}
