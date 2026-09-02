using System;

namespace AttendanceSystem.Application.Common
{
    public interface IClock
    {
        DateTime UtcNow { get; }
        DateTime LocalNow { get; } // UTC+8 local time
        DateOnly TodayLocal { get; } // DateOnly.FromDateTime(LocalNow)
    }
}