using System;
using AttendanceSystem.Application.Common;

namespace AttendanceSystem.Infrastructure.Services
{
    public class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;

        public DateTime LocalNow => AttendanceTimeZone.ToLocalTime(UtcNow);

        public DateOnly TodayLocal => DateOnly.FromDateTime(LocalNow);
    }
}