using Microsoft.EntityFrameworkCore;


namespace AttendIQ.Api.Services;


using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Infrastructure.Persistence;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public sealed class EmployeeReportDto
{
    public int TotalWorkDays { get; set; }
    public int PresentDays { get; set; }
    public int LateDays { get; set; }
    public int AbsentDays { get; set; }
    public double OvertimeHours { get; set; }
    public double UndertimeHours { get; set; }
    public int LeaveDays { get; set; }
    public int LeaveBalance { get; set; }
    public double AttendanceRate { get; set; }
    public double PunctualityRate { get; set; }
    public string? AvgCheckIn { get; set; }
    public string? AvgCheckOut { get; set; }
    public int MaxLateMinutes { get; set; }
    public int ConsecutivePresentDays { get; set; }
    public List<AttendanceRecordDto> AttendanceRecords { get; set; } = [];
    public List<LeaveRequestDto> LeaveRequests { get; set; } = [];
    public List<LeaveBalanceDto> LeaveBalances { get; set; } = [];
}

public sealed class AttendanceRecordDto
{
    public DateOnly Date { get; set; }
    public TimeOnly? CheckIn { get; set; }
    public TimeOnly? CheckOut { get; set; }
    public double? WorkedHours { get; set; }
    public double? OvertimeHours { get; set; }
    public double? UndertimeHours { get; set; }
    public string Status { get; set; } = "";
    public string? Note { get; set; }
}

public sealed class LeaveRequestDto
{
    public DateTime RequestedAt { get; set; }
    public string LeaveType { get; set; } = "";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int Days { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "";
}

public sealed class LeaveBalanceDto
{
    public string LeaveType { get; set; } = "";
    public int Total { get; set; }
    public int Used { get; set; }
    public int Remaining { get; set; }
}

// ── Interface ────────────────────────────────────────────────────────────────

public interface IEmployeeReportService
{
    Task<EmployeeReportDto?> GetAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct);
    Task<byte[]?> ExportPdfAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct);
    Task<byte[]?> ExportExcelAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct);
}

// ── Implementation ────────────────────────────────────────────────────────────

public sealed class EmployeeReportService(ApplicationDbContext db) : IEmployeeReportService
{
    private const int AnnualLeaveTotal = 15;

    public async Task<EmployeeReportDto?> GetAsync(
        Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var employee = await db.Employees
            .Include(e => e.WorkSchedule)
            .FirstOrDefaultAsync(e => e.Id == employeeId, ct);
        if (employee is null) return null;

        // ── Attendance records ──
        var records = await db.AttendanceRecords
            .Where(a => a.EmployeeId == employeeId && a.Date >= from && a.Date <= to)
            .OrderBy(a => a.Date)
            .ToListAsync(ct);

        // ── Leave requests ──
        var leaves = await db.LeaveRequests
            .Where(l => l.EmployeeId == employeeId && l.StartDate <= to && l.EndDate >= from)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

        // ── Build attendance DTOs ──
        int totalWorkDays = CountWorkDays(from, to);
        var attendanceDtos = new List<AttendanceRecordDto>();

        foreach (var r in records)
        {
            double? worked = null;
            double? overtime = null;
            double? undertime = null;

            // ✅ FIXED: CheckOutTime is DateTime? (nullable), CheckInTime is DateTime (not nullable)
            if (r.CheckOutTime.HasValue)
            {
                worked = (r.CheckOutTime.Value - r.CheckInTime).TotalHours;
                var standardHours = (double)(employee.WorkSchedule?.StandardHoursPerDay ?? 8m);
                var diff = worked.Value - standardHours;
                if (diff > 0) overtime = (double)r.OvertimeHours;
                else if (diff < 0) undertime = Math.Abs(diff);
            }

            attendanceDtos.Add(new AttendanceRecordDto
            {
                Date = r.Date,
                CheckIn = TimeOnly.FromDateTime(r.CheckInTime),  // ✅ CheckInTime is always present
                CheckOut = r.CheckOutTime.HasValue ? TimeOnly.FromDateTime(r.CheckOutTime.Value) : null,  // ✅ CheckOutTime is nullable
                WorkedHours = worked,
                OvertimeHours = overtime,
                UndertimeHours = undertime,
                Status = r.Status.ToString(),  // ✅ Status is enum, not nullable
                Note = r.Notes  // ✅ Property is "Notes" not "Note"
            });
        }

        // ── Summary stats ──
        int presentDays = records.Count(r => r.Status != AttendanceStatus.Absent);
        int lateDays = records.Count(r => r.LateMinutes > 0);  // ✅ LateMinutes is decimal, not nullable
        int absentDays = totalWorkDays - presentDays;

        double overtimeTotal = records.Sum(r => (double)r.OvertimeHours);  // ✅ OvertimeHours is decimal, not nullable
        double undertimeTotal = attendanceDtos.Sum(r => r.UndertimeHours ?? 0);

        // ✅ Use RequestStatus enum
        int approvedLeaveDays = leaves
            .Where(l => l.Status == RequestStatus.Approved)
            .Sum(l => CalculateLeaveDays(l));

        double attendanceRate = totalWorkDays > 0 ? (double)presentDays / totalWorkDays * 100 : 0;
        double punctualityRate = presentDays > 0 ? (double)(presentDays - lateDays) / presentDays * 100 : 0;

        // Avg check-in / check-out
        var checkins = records
            .Select(r => TimeOnly.FromDateTime(r.CheckInTime))
            .ToList();
        var checkouts = records
            .Where(r => r.CheckOutTime.HasValue)  // ✅ Only nullable CheckOutTime
            .Select(r => TimeOnly.FromDateTime(r.CheckOutTime!.Value))
            .ToList();

        string? avgIn = checkins.Count > 0
            ? TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(checkins.Average(t => t.ToTimeSpan().TotalMinutes))).ToString(@"HH\:mm")
            : null;
        string? avgOut = checkouts.Count > 0
            ? TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(checkouts.Average(t => t.ToTimeSpan().TotalMinutes))).ToString(@"HH\:mm")
            : null;

        // Max late minutes
        int maxLate = records
            .Select(r => (int)r.LateMinutes)  // ✅ LateMinutes is decimal, convert to int
            .DefaultIfEmpty(0)
            .Max();

        // Consecutive present days (current streak up to today)
        int streak = 0;
        foreach (var r in attendanceDtos.OrderByDescending(x => x.Date))
        {
            if (r.Status != AttendanceStatus.Absent.ToString())
                streak++;
            else
                break;
        }

        // ── Leave balance DTOs ──
        var balanceDtos = new List<LeaveBalanceDto>
        {
            new LeaveBalanceDto
            {
                LeaveType = LeaveType.Annual.ToString(),
                Total = AnnualLeaveTotal,
                Used = approvedLeaveDays,
                Remaining = Math.Max(0, AnnualLeaveTotal - approvedLeaveDays)
            }
        };

        int leaveBalance = balanceDtos.Sum(b => b.Remaining);

        return new EmployeeReportDto
        {
            TotalWorkDays = totalWorkDays,
            PresentDays = presentDays,
            LateDays = lateDays,
            AbsentDays = absentDays,
            OvertimeHours = Math.Round(overtimeTotal, 1),
            UndertimeHours = Math.Round(undertimeTotal, 1),
            LeaveDays = approvedLeaveDays,
            LeaveBalance = leaveBalance,
            AttendanceRate = Math.Round(attendanceRate, 1),
            PunctualityRate = Math.Round(punctualityRate, 1),
            AvgCheckIn = avgIn,
            AvgCheckOut = avgOut,
            MaxLateMinutes = maxLate,
            ConsecutivePresentDays = streak,
            AttendanceRecords = attendanceDtos,
            LeaveRequests = leaves.Select(l => new LeaveRequestDto
            {
                RequestedAt = l.CreatedAt,
                LeaveType = l.LeaveType.ToString(),
                StartDate = l.StartDate,
                EndDate = l.EndDate,
                Days = CalculateLeaveDays(l),
                Reason = l.Reason,
                Status = l.Status.ToString()
            }).ToList(),
            LeaveBalances = balanceDtos
        };
    }

    public Task<byte[]?> ExportPdfAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        throw new NotImplementedException("PDF export хэрэгжүүлэгдээгүй байна.");
    }

    public Task<byte[]?> ExportExcelAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        throw new NotImplementedException("Excel export хэрэгжүүлэгдээгүй байна.");
    }

   
    // ── Helpers ──────────────────────────────────────────────────────────

    private static int CountWorkDays(DateOnly from, DateOnly to)
    {
        int count = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
            if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                count++;
        return count;
    }

    private static int CalculateLeaveDays(dynamic leaveRequest)
    {
        try
        {
            var startDate = (DateOnly)leaveRequest.StartDate;
            var endDate = (DateOnly)leaveRequest.EndDate;

            if (leaveRequest.LeaveMode != null && leaveRequest.LeaveMode == "Hourly" && leaveRequest.Hours != null)
            {
                return 0;
            }

            int days = 0;
            for (var d = startDate; d <= endDate; d = d.AddDays(1))
                if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                    days++;
            return days;
        }
        catch
        {
            return 0;
        }
    }
}
