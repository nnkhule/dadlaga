using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public DashboardApiController(ApplicationDbContext db) => _db = db;

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryApiDto>> Summary([FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.Now);
        var totalEmployees = await _db.Employees.AsNoTracking().CountAsync(cancellationToken);
        var activeEmployees = await _db.Employees.AsNoTracking().CountAsync(e => e.IsActive, cancellationToken);

        var todayRecords = _db.AttendanceRecords.AsNoTracking().Where(a => a.Date == targetDate);
        // Present bucket: Present, EarlyLeave, HalfDay, NightShift, WeekendWork (excludes Late)
        var presentBucketToday = await todayRecords.CountAsync(a =>
            a.Status == AttendanceStatus.Present ||
            a.Status == AttendanceStatus.EarlyLeave ||
            a.Status == AttendanceStatus.HalfDay ||
            a.Status == AttendanceStatus.NightShift ||
            a.Status == AttendanceStatus.WeekendWork,
            cancellationToken);
        // Late: only those with Status == Late
        var lateEmployees = await todayRecords.CountAsync(a =>
            a.Status == AttendanceStatus.Late,
            cancellationToken);
        // Total checked in = Present bucket + Late
        var checkedInToday = presentBucketToday + lateEmployees;


var onLeaveEmployees = await _db.LeaveRequests
    .AsNoTracking()
    .CountAsync(l =>
        l.Status == RequestStatus.Approved &&
        l.StartDate <= targetDate &&
        l.EndDate >= targetDate,
        cancellationToken);

        var absentToday =
            activeEmployees -
            checkedInToday -
            onLeaveEmployees;


    var overtimeHours = await todayRecords.SumAsync(
        a => a.OvertimeHours,
        cancellationToken);

if (absentToday < 0)
    absentToday = 0;

        // attendanceRate = (Present bucket + Late) / актив ажилтнууд * 100
        var attendanceRate = activeEmployees == 0
            ? 0
            : Math.Round((decimal)checkedInToday / activeEmployees * 100, 2);
        attendanceRate = Math.Min(attendanceRate, 100); // Ensure not over 100%
        return Ok(new DashboardSummaryApiDto(
            totalEmployees,
            activeEmployees,
            presentBucketToday,   // Present today (present bucket only)
            absentToday,
            lateEmployees,        // Late employees
            onLeaveEmployees,
            attendanceRate,
            overtimeHours));
    }

    [HttpGet("recent-activities")]
    public async Task<ActionResult<PagedResponseDto<RecentActivityApiDto>>> RecentActivities(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _db.AttendanceRecords
            .AsNoTracking()
            .Include(a => a.Employee)
            .OrderByDescending(a => a.UpdatedAt ?? a.CreatedAt)
            .ThenByDescending(a => a.CheckInTime);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new RecentActivityApiDto(
                a.Id,
                "Attendance",
                a.Employee == null ? "Attendance record" : a.Employee.FullName,
                a.CheckOutTime == null ? "Checked in" : "Checked out",
                a.UpdatedAt ?? a.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponseDto<RecentActivityApiDto>(items, pageNumber, pageSize, total));
    }

    [HttpGet("statistics")]
    public async Task<ActionResult<AttendanceTrendApiDto>> Statistics([FromQuery] int days = 7, CancellationToken cancellationToken = default)
    {
        days = Math.Clamp(days, 1, 31);
        var today = DateOnly.FromDateTime(DateTime.Now);
        var from = today.AddDays(-(days - 1));
        var activeEmployees = await _db.Employees.AsNoTracking().CountAsync(e => e.IsActive, cancellationToken);
        var records = await _db.AttendanceRecords
            .AsNoTracking()
            .Where(a => a.Date >= from && a.Date <= today)
            .GroupBy(a => a.Date)
            .Select(g => new
            {
                Date    = g.Key,
                Present = g.Count(a => a.Status == AttendanceStatus.Present ||
                                       a.Status == AttendanceStatus.EarlyLeave ||
                                       a.Status == AttendanceStatus.HalfDay ||
                                       a.Status == AttendanceStatus.NightShift ||
                                       a.Status == AttendanceStatus.WeekendWork),
                Late    = g.Count(a => a.Status == AttendanceStatus.Late)
            })
            .ToListAsync(cancellationToken);

        // Get OnLeave counts from LeaveRequests for each date
        var onLeaveRecords = await _db.LeaveRequests
            .AsNoTracking()
            .Where(l => l.Status == RequestStatus.Approved &&
                        l.StartDate <= today &&
                        l.EndDate >= from)
            .ToListAsync(cancellationToken);

        var onLeaveCounts = new Dictionary<DateOnly, int>();
        foreach (var leave in onLeaveRecords)
        {
            // For each day the leave is active, increment the count
            var currentDate = leave.StartDate;
            while (currentDate <= leave.EndDate)
            {
                if (currentDate >= from && currentDate <= today)
                {
                    if (!onLeaveCounts.ContainsKey(currentDate))
                        onLeaveCounts[currentDate] = 0;
                    onLeaveCounts[currentDate]++;
                }
                currentDate = currentDate.AddDays(1);
            }
        }

        var labels  = new List<string>();
        var present = new List<int>();
        var absent  = new List<int>();
        var late    = new List<int>();
        var onLeave = new List<int>();

        for (var date = from; date <= today; date = date.AddDays(1))
        {
            var row = records.FirstOrDefault(x => x.Date == date);
            int presentCount = row?.Present ?? 0;
            int lateCount = row?.Late ?? 0;
            int onLeaveCount = onLeaveCounts.TryGetValue(date, out var count) ? count : 0;

            labels.Add(date.ToString("yyyy-MM-dd"));
            present.Add(presentCount);
            absent.Add(Math.Max(0, activeEmployees - (presentCount + lateCount + onLeaveCount)));
            late.Add(lateCount);
            onLeave.Add(onLeaveCount);
        }

        return Ok(new AttendanceTrendApiDto(labels, present, absent, late, onLeave));
    }
}

public sealed record DashboardSummaryApiDto(
    int TotalEmployees,
    int ActiveEmployees,
    int PresentToday,
    int AbsentToday,
    int LateEmployees,
    int OnLeaveEmployees,
    decimal AttendanceRate,
    decimal TotalOvertimeHours);

public sealed record RecentActivityApiDto(Guid Id, string Type, string Title, string Description, DateTime CreatedAt);
public sealed record AttendanceTrendApiDto(IReadOnlyList<string> Labels, IReadOnlyList<int> PresentCounts, IReadOnlyList<int> AbsentCounts, IReadOnlyList<int> LateCounts, IReadOnlyList<int> OnLeaveCounts);