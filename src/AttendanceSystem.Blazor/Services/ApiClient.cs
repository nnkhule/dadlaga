using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AttendanceSystem.Application.DTOs.AI;
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

    public async Task<HttpResponseMessage> DeleteAsync(string url, CancellationToken cancellationToken = default)
    {
        return await SendWithRefreshAsync(() => _http.DeleteAsync(url, cancellationToken));
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

        var count = new[] { response.Labels.Count, response.PresentCounts.Count, response.AbsentCounts.Count, response.LateCounts.Count, response.OnLeaveCounts.Count }.Min();
        var items = new List<AttendanceTrendDto>();
        for (var i = 0; i < count; i++)
        {
            var date = DateOnly.TryParse(response.Labels[i], out var parsed)
                ? parsed
                : DateOnly.FromDateTime(DateTime.Today.AddDays(i - count + 1));

            items.Add(new AttendanceTrendDto(date, response.PresentCounts[i], response.AbsentCounts[i], response.LateCounts[i], response.OnLeaveCounts[i]));
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

    public Task<UnreadNotificationCountDto?> GetUnreadNotificationCountAsync()
        => GetAsync<UnreadNotificationCountDto>("api/notifications/unread-count");

    public async Task<IReadOnlyList<NotificationDto>> GetRecentNotificationsAsync(int pageSize = 5)
    {
        var response = await GetAsync<PagedResponse<NotificationDto>>($"api/notifications?pageNumber=1&pageSize={pageSize}");
        return response?.Items ?? [];
    }

    public Task<HttpResponseMessage> MarkNotificationReadAsync(Guid id)
        => PostAsync($"api/notifications/{id}/read", new { });

    public Task<OvertimeSummaryDto?> GetOvertimeSummaryAsync(DateOnly from, DateOnly to)
        => GetAsync<OvertimeSummaryDto>($"api/reports/overtime-summary?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}");

    public Task<HttpResponseMessage> ApproveLeaveRequestAsync(Guid id)
        => PostAsync($"api/leave/requests/{id}/approve", new { });

    public Task<HttpResponseMessage> RejectLeaveRequestAsync(Guid id)
        => PostAsync($"api/leave/requests/{id}/reject", new { });

    public Task<HttpResponseMessage> DeactivateEmployeeAsync(Guid id)
        => DeleteAsync($"api/employees/{id}");

    public Task<HttpResponseMessage> ReactivateEmployeeAsync(Guid id)
        => PostAsync($"api/employees/{id}/reactivate", new { });

    public async Task<ChatResponseDto?> PostChatAsync(ChatRequestDto request, CancellationToken cancellationToken = default)
    {
        var response = await PostAsync("api/ai/chat", request, cancellationToken);
        return await response.Content.ReadFromJsonAsync<ChatResponseDto>(cancellationToken: cancellationToken);
    }

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
            if (response.IsSuccessStatusCode)
                return response;

            await ThrowHttpRequestExceptionAsync(response);
        }

        response.Dispose();
        if (await _auth.RefreshTokenAsync())
        {
            await AuthorizeAsync();
            response = await send();
            if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
            {
                if (response.IsSuccessStatusCode)
                    return response;

                await ThrowHttpRequestExceptionAsync(response);
            }

            response.Dispose();
        }

        await _auth.LogoutAsync();
        _navigation.NavigateTo("/login", forceLoad: true);
        throw new UnauthorizedAccessException("Your session has expired. Please sign in again.");
    }

    private static async Task ThrowHttpRequestExceptionAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var message = content;

        try
        {
            var error = JsonSerializer.Deserialize<ApiErrorResponse>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (error is not null)
            {
                message = error.Message ?? error.Error ?? error.Detail ?? content;
            }
        }
        catch
        {
            // Ignore parsing failures and preserve raw content.
        }

        response.Dispose();
        throw new HttpRequestException($"Request failed with status code {(int)response.StatusCode}: {message}");
    }

    private sealed record ApiErrorResponse(string? Message, string? Error, string? Detail, string? Code);

    public Task<EmployeeStatisticsDto?> GetEmployeeStatisticsAsync()
        => GetAsync<EmployeeStatisticsDto>("api/statistics/employee");

    private sealed record RecentActivityResponse(Guid Id, string Type, string Title, string? Description, DateTime CreatedAt);
    private sealed record AttendanceTrendResponse(IReadOnlyList<string> Labels, IReadOnlyList<int> PresentCounts, IReadOnlyList<int> AbsentCounts, IReadOnlyList<int> LateCounts, IReadOnlyList<int> OnLeaveCounts);
}