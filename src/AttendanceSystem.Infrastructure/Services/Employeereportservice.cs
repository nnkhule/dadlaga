
using System.Text;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Services;

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

public interface IEmployeeReportService
{
    Task<EmployeeReportDto?> GetAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct);
    Task<byte[]?> ExportPdfAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct);
    Task<byte[]?> ExportExcelAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct);
}

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

        var records = await db.AttendanceRecords
            .Where(a => a.EmployeeId == employeeId && a.Date >= from && a.Date <= to)
            .OrderBy(a => a.Date)
            .ToListAsync(ct);

        var leaves = await db.LeaveRequests
            .Where(l => l.EmployeeId == employeeId && l.StartDate <= to && l.EndDate >= from)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

        int totalWorkDays = CountWorkDays(from, to);
        var attendanceDtos = new List<AttendanceRecordDto>();

        foreach (var record in records)
        {
            double? worked = null;
            double? overtime = null;
            double? undertime = null;

            if (record.CheckOutTime.HasValue)
            {
                worked = (record.CheckOutTime.Value - record.CheckInTime).TotalHours;
                var standardHours = (double)(employee.WorkSchedule?.StandardHoursPerDay ?? 8m);
                var diff = worked.Value - standardHours;
                if (diff > 0) overtime = (double)record.OvertimeHours;
                else if (diff < 0) undertime = Math.Abs(diff);
            }

            attendanceDtos.Add(new AttendanceRecordDto
            {
                Date = record.Date,
                CheckIn = TimeOnly.FromDateTime(record.CheckInTime),
                CheckOut = record.CheckOutTime.HasValue
                    ? TimeOnly.FromDateTime(record.CheckOutTime.Value)
                    : null,
                WorkedHours = worked,
                OvertimeHours = overtime,
                UndertimeHours = undertime,
                Status = record.Status.ToString(),
                Note = record.Notes
            });
        }

        int presentDays = records.Count(r => r.Status is AttendanceStatus.Present
            or AttendanceStatus.EarlyLeave
            or AttendanceStatus.HalfDay
            or AttendanceStatus.NightShift
            or AttendanceStatus.WeekendWork);
        int lateDays = records.Count(r => r.Status == AttendanceStatus.Late);

        int approvedLeaveDays = leaves
            .Where(l => l.Status == RequestStatus.Approved)
            .Sum(CalculateLeaveDays);

        int absentDays = Math.Max(0, totalWorkDays - (presentDays + lateDays + approvedLeaveDays));
        double overtimeTotal = records.Sum(r => (double)r.OvertimeHours);
        double undertimeTotal = attendanceDtos.Sum(r => r.UndertimeHours ?? 0);

        double attendanceRate = totalWorkDays > 0 ? (double)(presentDays + lateDays) / totalWorkDays * 100 : 0;
        double punctualityRate = presentDays > 0 ? (double)(presentDays - lateDays) / presentDays * 100 : 0;

        var checkins = records
            .Select(r => TimeOnly.FromDateTime(r.CheckInTime))
            .ToList();
        var checkouts = records
            .Where(r => r.CheckOutTime.HasValue)
            .Select(r => TimeOnly.FromDateTime(r.CheckOutTime!.Value))
            .ToList();

        string? avgIn = checkins.Count > 0
            ? TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(checkins.Average(t => t.ToTimeSpan().TotalMinutes))).ToString(@"HH\:mm")
            : null;
        string? avgOut = checkouts.Count > 0
            ? TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(checkouts.Average(t => t.ToTimeSpan().TotalMinutes))).ToString(@"HH\:mm")
            : null;

        int maxLate = records
            .Select(r => (int)r.LateMinutes)
            .DefaultIfEmpty(0)
            .Max();

        int streak = 0;
        foreach (var record in attendanceDtos.OrderByDescending(x => x.Date))
        {
            if (record.Status != AttendanceStatus.Absent.ToString())
                streak++;
            else
                break;
        }

        var balanceDtos = new List<LeaveBalanceDto>
        {
            new()
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
        => ExportTextAsync(employeeId, from, to, ct);

    public Task<byte[]?> ExportExcelAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct)
        => ExportCsvAsync(employeeId, from, to, ct);

    private async Task<byte[]?> ExportCsvAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var report = await GetAsync(employeeId, from, to, ct);
        if (report is null) return null;

        var builder = new StringBuilder();
        builder.AppendLine("Date,CheckIn,CheckOut,WorkedHours,OvertimeHours,UndertimeHours,Status,Note");
        foreach (var record in report.AttendanceRecords)
        {
            builder.AppendLine(string.Join(",", new[]
            {
                EscapeCsv(record.Date.ToString("yyyy-MM-dd")),
                EscapeCsv(record.CheckIn?.ToString(@"HH\:mm")),
                EscapeCsv(record.CheckOut?.ToString(@"HH\:mm")),
                EscapeCsv(record.WorkedHours?.ToString("F1")),
                EscapeCsv(record.OvertimeHours?.ToString("F1")),
                EscapeCsv(record.UndertimeHours?.ToString("F1")),
                EscapeCsv(record.Status),
                EscapeCsv(record.Note)
            }));
        }

        return WithUtf8Bom(builder.ToString());
    }

    private async Task<byte[]?> ExportTextAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var report = await GetAsync(employeeId, from, to, ct);
        if (report is null) return null;

        var builder = new StringBuilder();
        builder.AppendLine($"Employee report: {from:yyyy-MM-dd} - {to:yyyy-MM-dd}");
        builder.AppendLine($"Work days: {report.TotalWorkDays}");
        builder.AppendLine($"Present: {report.PresentDays}");
        builder.AppendLine($"Late: {report.LateDays}");
        builder.AppendLine($"Absent: {report.AbsentDays}");
        builder.AppendLine($"Overtime hours: {report.OvertimeHours:F1}");
        builder.AppendLine($"Undertime hours: {report.UndertimeHours:F1}");
        builder.AppendLine();
        builder.AppendLine("Date\tCheck in\tCheck out\tWorked\tOvertime\tUndertime\tStatus\tNote");

        foreach (var record in report.AttendanceRecords)
        {
            builder.AppendLine(string.Join('\t', new[]
            {
                record.Date.ToString("yyyy-MM-dd"),
                record.CheckIn?.ToString(@"HH\:mm") ?? "",
                record.CheckOut?.ToString(@"HH\:mm") ?? "",
                record.WorkedHours?.ToString("F1") ?? "",
                record.OvertimeHours?.ToString("F1") ?? "",
                record.UndertimeHours?.ToString("F1") ?? "",
                record.Status,
                record.Note ?? ""
            }));
        }

        return WithUtf8Bom(builder.ToString());
    }

    private static int CountWorkDays(DateOnly from, DateOnly to)
    {
        int count = 0;
        for (var date = from; date <= to; date = date.AddDays(1))
            if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                count++;
        return count;
    }

    private static int CalculateLeaveDays(LeaveRequest leaveRequest)
    {
        if (leaveRequest.LeaveMode == "Hourly" && leaveRequest.Hours.HasValue)
        {
            return 0;
        }

        int days = 0;
        for (var date = leaveRequest.StartDate; date <= leaveRequest.EndDate; date = date.AddDays(1))
            if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                days++;
        return days;
    }

    private static string EscapeCsv(string? value)
    {
        value ??= string.Empty;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private static byte[] WithUtf8Bom(string value)
        => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(value)).ToArray();
}