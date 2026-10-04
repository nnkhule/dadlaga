namespace AttendanceSystem.Application.Common;

public static class AttendanceTimeZone
{
    private static readonly TimeZoneInfo Ulaanbaatar = FindUlaanbaatarTimeZone();

    public static DateTime ToLocalTime(DateTime value)
    {
        if (value.Kind == DateTimeKind.Utc)
            return TimeZoneInfo.ConvertTimeFromUtc(value, Ulaanbaatar);

        return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
    }

    private static TimeZoneInfo FindUlaanbaatarTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ulaanbaatar");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Ulaanbaatar Standard Time");
        }
    }
}
