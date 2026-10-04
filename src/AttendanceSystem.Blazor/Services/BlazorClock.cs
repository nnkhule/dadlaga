using AttendanceSystem.Application.Common;

namespace AttendanceSystem.Blazor.Services;

public sealed class BlazorClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime LocalNow => AttendanceTimeZone.ToLocalTime(UtcNow);

    public DateOnly TodayLocal => DateOnly.FromDateTime(LocalNow);
}
