namespace AttendanceSystem.Domain.Enums;

/// <summary>
/// Single source of truth for how raw <see cref="AttendanceStatus"/> values stored on
/// <c>AttendanceRecord.Status</c> are grouped into the buckets used by dashboards, KPI cards,
/// charts and reports.
///
/// IMPORTANT: No other layer (API, Blazor, JS) should re-derive attendance status from
/// CheckInTime/CheckOutTime. The stored <c>AttendanceRecord.Status</c> is authoritative and
/// every consumer must classify it using this class so the table, KPI cards and charts always
/// agree for the same employee/day.
/// </summary>
public static class AttendanceStatusClassifier
{
    /// <summary>
    /// Statuses that represent an employee who showed up and is counted in the
    /// "Present" KPI/donut bucket. Deliberately EXCLUDES <see cref="AttendanceStatus.Late"/>,
    /// which is tracked as its own mutually-exclusive bucket.
    /// </summary>
    public static readonly AttendanceStatus[] PresentBucket =
    {
        AttendanceStatus.Present,
        AttendanceStatus.EarlyLeave,
        AttendanceStatus.NightShift,
        AttendanceStatus.WeekendWork,
        AttendanceStatus.HalfDay
    };

    public static bool IsPresentBucket(AttendanceStatus status) =>
        Array.IndexOf(PresentBucket, status) >= 0;

    public static bool IsLate(AttendanceStatus status) =>
        status == AttendanceStatus.Late;

    public static bool IsOnLeave(AttendanceStatus status) =>
        status == AttendanceStatus.OnLeave;

    public static bool IsAbsent(AttendanceStatus status) =>
        status == AttendanceStatus.Absent;

    /// <summary>
    /// "Attended" = showed up at all, whether on time or late. Used for AttendanceRate,
    /// where Present and Late are combined but OnLeave/Absent are not.
    /// Present bucket and Late are mutually exclusive, so this never double counts.
    /// </summary>
    public static bool CountsAsAttended(AttendanceStatus status) =>
        IsPresentBucket(status) || IsLate(status);

    public static string ToDisplayName(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Present => "Present",
        AttendanceStatus.Late => "Late",
        AttendanceStatus.EarlyLeave => "EarlyLeave",
        AttendanceStatus.Absent => "Absent",
        AttendanceStatus.OnLeave => "OnLeave",
        AttendanceStatus.Holiday => "Holiday",
        AttendanceStatus.NightShift => "NightShift",
        AttendanceStatus.WeekendWork => "WeekendWork",
        AttendanceStatus.HalfDay => "HalfDay",
        AttendanceStatus.PendingManualReview => "PendingManualReview",
        _ => status.ToString()
    };
}