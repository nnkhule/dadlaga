using System.Text;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ClosedXML.Excel;

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
        => GeneratePdfAsync(employeeId, from, to, ct);

    public Task<byte[]?> ExportExcelAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct)
        => GenerateExcelAsync(employeeId, from, to, ct);

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

    private async Task<byte[]?> GeneratePdfAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var report = await GetAsync(employeeId, from, to, ct);
        if (report is null) return null;

        var employee = await db.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == employeeId, ct);

        var employeeName = employee?.FullName ?? "Тодорхойгүй ажилтан";
        var employeeCode = employee?.EmployeeCode ?? "—";

        var summaryItems = new[]
        {
            ("Нийт өдөр", report.TotalWorkDays.ToString(), Colors.Blue.Medium),
            ("Ирсэн", report.PresentDays.ToString(), Colors.Green.Medium),
            ("Хоцорсон", report.LateDays.ToString(), Colors.Orange.Medium),
            ("Тасалсан", report.AbsentDays.ToString(), Colors.Red.Medium),
            ("Илүү цаг", $"{report.OvertimeHours:F1}", Colors.Purple.Medium),
            ("Чөлөө", $"{report.LeaveDays}", Colors.Teal.Medium)
        };

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.2f, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Darken3));

                page.Header().PaddingBottom(14).Row(row =>
                {
                    row.ConstantItem(84).Height(46).Background(Colors.Blue.Darken2).AlignCenter().AlignMiddle()
                        .Text("ATTENDIQ")
                        .FontColor(Colors.White)
                        .SemiBold()
                        .FontSize(16);

                    row.RelativeItem().PaddingLeft(12).Column(col =>
                    {
                        col.Item().Text("Ажилтны тайлан").SemiBold().FontColor(Colors.Blue.Darken2).FontSize(18);
                        col.Item().Text($"{employeeName} • {employeeCode}").FontSize(10).FontColor(Colors.Grey.Darken1);
                        col.Item().Text($"{from:yyyy-MM-dd} - {to:yyyy-MM-dd}").FontSize(10).FontColor(Colors.Grey.Darken1);
                    });

                    row.ConstantItem(120).AlignRight().Column(col =>
                    {
                        col.Item().Text("Ирц").FontSize(8).FontColor(Colors.Grey.Darken1);
                        col.Item().Text($"{report.AttendanceRate:F1}%").SemiBold().FontSize(20).FontColor(Colors.Green.Darken2);
                    });
                });

                page.Content().Column(column =>
                {
                    column.Item().PaddingBottom(10).Row(row =>
                    {
                        foreach (var (label, value, color) in summaryItems)
                        {
                            row.RelativeItem().Padding(4).Column(card =>
                            {
                                card.Item().Background(color).Padding(8).Text(label)
                                    .FontSize(8)
                                    .FontColor(Colors.White)
                                    .SemiBold();

                                card.Item().Background(Colors.Grey.Lighten4).Padding(8)
                                    .Text(value)
                                    .FontSize(18)
                                    .SemiBold()
                                    .FontColor(Colors.Grey.Darken3)
                                    .AlignCenter();
                            });
                        }
                    });

                    column.Item().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem().Background(Colors.Grey.Lighten4).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
                        {
                            col.Item().Text("Нийт ажилласан цаг").FontSize(8).FontColor(Colors.Grey.Darken1);
                            col.Item().Text($"{report.OvertimeHours:F1} цаг").SemiBold().FontSize(16).FontColor(Colors.Grey.Darken3);
                        });

                        row.RelativeItem().Background(Colors.Grey.Lighten4).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
                        {
                            col.Item().Text("Дундаж ирсэн цаг").FontSize(8).FontColor(Colors.Grey.Darken1);
                            col.Item().Text(report.AvgCheckIn ?? "N/A").SemiBold().FontSize(16).FontColor(Colors.Grey.Darken3);
                        });

                        row.RelativeItem().Background(Colors.Grey.Lighten4).Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
                        {
                            col.Item().Text("Дундаж гарсан цаг").FontSize(8).FontColor(Colors.Grey.Darken1);
                            col.Item().Text(report.AvgCheckOut ?? "N/A").SemiBold().FontSize(16).FontColor(Colors.Grey.Darken3);
                        });
                    });

                    column.Item().PaddingTop(14).Text("Ирцийн түүх").SemiBold().FontSize(12).FontColor(Colors.Grey.Darken3);

                    if (report.AttendanceRecords.Any())
                    {
                        IContainer HeaderCellStyle(IContainer container) =>
                            container.Background(Colors.Blue.Darken2)
                                .Padding(6)
                                .AlignCenter()
                                .AlignMiddle();

                        IContainer CellStyle(IContainer container) =>
                            container.Border(0.5f)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Padding(5)
                                .AlignMiddle();

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.3f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(cell =>
                                {
                                    cell.Background(Colors.Blue.Darken2).Padding(6).AlignCenter().AlignMiddle();
                                    cell.Text(text => text.Span("Огноо").FontColor(Colors.White).FontSize(8).SemiBold());
                                });
                                header.Cell().Element(cell =>
                                {
                                    cell.Background(Colors.Blue.Darken2).Padding(6).AlignCenter().AlignMiddle();
                                    cell.Text(text => text.Span("Ирсэн").FontColor(Colors.White).FontSize(8).SemiBold());
                                });
                                header.Cell().Element(cell =>
                                {
                                    cell.Background(Colors.Blue.Darken2).Padding(6).AlignCenter().AlignMiddle();
                                    cell.Text(text => text.Span("Гарсан").FontColor(Colors.White).FontSize(8).SemiBold());
                                });
                                header.Cell().Element(cell =>
                                {
                                    cell.Background(Colors.Blue.Darken2).Padding(6).AlignCenter().AlignMiddle();
                                    cell.Text(text => text.Span("Ажил").FontColor(Colors.White).FontSize(8).SemiBold());
                                });
                                header.Cell().Element(cell =>
                                {
                                    cell.Background(Colors.Blue.Darken2).Padding(6).AlignCenter().AlignMiddle();
                                    cell.Text(text => text.Span("Илүү").FontColor(Colors.White).FontSize(8).SemiBold());
                                });
                                header.Cell().Element(cell =>
                                {
                                    cell.Background(Colors.Blue.Darken2).Padding(6).AlignCenter().AlignMiddle();
                                    cell.Text(text => text.Span("Дутуу").FontColor(Colors.White).FontSize(8).SemiBold());
                                });
                                header.Cell().Element(cell =>
                                {
                                    cell.Background(Colors.Blue.Darken2).Padding(6).AlignCenter().AlignMiddle();
                                    cell.Text(text => text.Span("Статус").FontColor(Colors.White).FontSize(8).SemiBold());
                                });
                            });

                            foreach (var record in report.AttendanceRecords)
                            {
                                table.Cell().Element(CellStyle).Text($"{record.Date:yyyy-MM-dd}");
                                table.Cell().Element(CellStyle).Text(record.CheckIn?.ToString(@"HH\:mm") ?? "—");
                                table.Cell().Element(CellStyle).Text(record.CheckOut?.ToString(@"HH\:mm") ?? "—");
                                table.Cell().Element(CellStyle).Text(record.WorkedHours.HasValue ? $"{record.WorkedHours:F1}h" : "—");
                                table.Cell().Element(CellStyle).Text(record.OvertimeHours.HasValue && record.OvertimeHours > 0 ? $"+{record.OvertimeHours:F1}" : "—");
                                table.Cell().Element(CellStyle).Text(record.UndertimeHours.HasValue && record.UndertimeHours > 0 ? $"-{record.UndertimeHours:F1}" : "—");
                                table.Cell().Element(CellStyle).Text(record.Status);
                            }
                        });
                    }
                    else
                    {
                        column.Item().PaddingVertical(8).Text("Энэ хугацаанд ирцийн мэдээлэл байхгүй байна.").FontColor(Colors.Grey.Darken1).Italic();
                    }

                    column.Item().PaddingTop(16).Text("Чөлөөний хүсэлт").SemiBold().FontSize(12).FontColor(Colors.Grey.Darken3);

                    if (report.LeaveRequests.Any())
                    {
                        IContainer HeaderCellStyle(IContainer container) =>
                            container.Background(Colors.Teal.Darken2)
                                .Padding(6)
                                .AlignCenter()
                                .AlignMiddle();

                        IContainer CellStyle(IContainer container) =>
                            container.Border(0.5f)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Padding(5)
                                .AlignMiddle();

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(0.7f);
                                columns.RelativeColumn(1.8f);
                                columns.RelativeColumn(1f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(cell =>
                                {
                                    cell.Background(Colors.Teal.Darken2).Padding(6).AlignCenter().AlignMiddle();
                                    cell.Text(text => text.Span("Огноо").FontColor(Colors.White).FontSize(8).SemiBold());
                                });
                                header.Cell().Element(cell =>
                                {
                                    cell.Background(Colors.Teal.Darken2).Padding(6).AlignCenter().AlignMiddle();
                                    cell.Text(text => text.Span("Төрөл").FontColor(Colors.White).FontSize(8).SemiBold());
                                });
                                header.Cell().Element(cell =>
                                {
                                    cell.Background(Colors.Teal.Darken2).Padding(6).AlignCenter().AlignMiddle();
                                    cell.Text(text => text.Span("Эхлэх").FontColor(Colors.White).FontSize(8).SemiBold());
                                });
                                header.Cell().Element(cell =>
                                {
                                    cell.Background(Colors.Teal.Darken2).Padding(6).AlignCenter().AlignMiddle();
                                    cell.Text(text => text.Span("Дуусах").FontColor(Colors.White).FontSize(8).SemiBold());
                                });
                                header.Cell().Element(cell =>
                                {
                                    cell.Background(Colors.Teal.Darken2).Padding(6).AlignCenter().AlignMiddle();
                                    cell.Text(text => text.Span("Өдөр").FontColor(Colors.White).FontSize(8).SemiBold());
                                });
                                header.Cell().Element(cell =>
                                {
                                    cell.Background(Colors.Teal.Darken2).Padding(6).AlignCenter().AlignMiddle();
                                    cell.Text(text => text.Span("Шалтгаан").FontColor(Colors.White).FontSize(8).SemiBold());
                                });
                                header.Cell().Element(cell =>
                                {
                                    cell.Background(Colors.Teal.Darken2).Padding(6).AlignCenter().AlignMiddle();
                                    cell.Text(text => text.Span("Төлөв").FontColor(Colors.White).FontSize(8).SemiBold());
                                });
                            });

                            foreach (var item in report.LeaveRequests)
                            {
                                table.Cell().Element(CellStyle).Text($"{item.RequestedAt:yyyy-MM-dd}");
                                table.Cell().Element(CellStyle).Text(item.LeaveType);
                                table.Cell().Element(CellStyle).Text($"{item.StartDate:yyyy-MM-dd}");
                                table.Cell().Element(CellStyle).Text($"{item.EndDate:yyyy-MM-dd}");
                                table.Cell().Element(CellStyle).Text(item.Days.ToString());
                                table.Cell().Element(CellStyle).Text(item.Reason ?? "—");
                                table.Cell().Element(CellStyle).Text(item.Status);
                            }
                        });
                    }
                    else
                    {
                        column.Item().PaddingVertical(8).Text("Энэ хугацаанд чөлөөний хүсэлт байхгүй байна.").FontColor(Colors.Grey.Darken1).Italic();
                    }

                    column.Item().PaddingTop(16).Text("Чөлөөний үлдэгдэл").SemiBold().FontSize(12).FontColor(Colors.Grey.Darken3);

                    if (report.LeaveBalances.Any())
                    {
                        IContainer HeaderCellStyle(IContainer container) =>
                            container.Background(Colors.Grey.Darken2)
                                .Padding(6)
                                .AlignCenter()
                                .AlignMiddle();

                        IContainer CellStyle(IContainer container) =>
                            container.Border(0.5f)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Padding(5)
                                .AlignMiddle();

                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2.5f);
                                columns.RelativeColumn(1f);
                                columns.RelativeColumn(1f);
                                columns.RelativeColumn(1f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(cell =>
                                {
                                    cell.Background(Colors.Grey.Darken2).Padding(6).AlignCenter().AlignMiddle();
                                    cell.Text(text => text.Span("Төрөл").FontColor(Colors.White).FontSize(8).SemiBold());
                                });
                                header.Cell().Element(cell =>
                                {
                                    cell.Background(Colors.Grey.Darken2).Padding(6).AlignCenter().AlignMiddle();
                                    cell.Text(text => text.Span("Нийт").FontColor(Colors.White).FontSize(8).SemiBold());
                                });
                                header.Cell().Element(cell =>
                                {
                                    cell.Background(Colors.Grey.Darken2).Padding(6).AlignCenter().AlignMiddle();
                                    cell.Text(text => text.Span("Ашигласан").FontColor(Colors.White).FontSize(8).SemiBold());
                                });
                                header.Cell().Element(cell =>
                                {
                                    cell.Background(Colors.Grey.Darken2).Padding(6).AlignCenter().AlignMiddle();
                                    cell.Text(text => text.Span("Үлдсэн").FontColor(Colors.White).FontSize(8).SemiBold());
                                });
                            });

                            foreach (var balance in report.LeaveBalances)
                            {
                                table.Cell().Element(CellStyle).Text(balance.LeaveType);
                                table.Cell().Element(CellStyle).Text(balance.Total.ToString());
                                table.Cell().Element(CellStyle).Text(balance.Used.ToString());
                                table.Cell().Element(CellStyle).Text(balance.Remaining.ToString());
                            }
                        });
                    }
                });

                page.Footer()
                    .AlignCenter()
                    .PaddingTop(10)
                    .Text(text =>
                    {
                        text.Span("Тайланг үүсгэсэн огноо: ").FontColor(Colors.Grey.Darken1).FontSize(8);
                        text.Span(DateTime.Today.ToString("yyyy-MM-dd")).FontColor(Colors.Grey.Darken1).FontSize(8);
                    });
            });
        })
        .GeneratePdf();
    }

    private async Task<byte[]?> GenerateExcelAsync(Guid employeeId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var report = await GetAsync(employeeId, from, to, ct);
        if (report is null) return null;

        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Employee Report");

        // Set up styles
        var headerStyle = worksheet.Style;
        headerStyle.Font.Bold = true;
        headerStyle.Fill.BackgroundColor = XLColor.LightGray;
        headerStyle.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerStyle.Border.BottomBorder = XLBorderStyleValues.Thin;

        var dataStyle = worksheet.Style;
        dataStyle.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        dataStyle.Border.BottomBorder = XLBorderStyleValues.Thin;

        int currentRow = 1;

        // Title
        worksheet.Cell(currentRow, 1).SetValue($"Employee Report: {from:yyyy-MM-dd} - {to:yyyy-MM-dd}");
        worksheet.Range(worksheet.Cell(currentRow, 1), worksheet.Cell(currentRow, 8)).Merge();
        worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
        worksheet.Cell(currentRow, 1).Style.Font.FontSize = 16;
        worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        currentRow += 2;

        // Summary section
        worksheet.Cell(currentRow, 1).SetValue("Summary");
        worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
        worksheet.Cell(currentRow, 1).Style.Font.FontSize = 12;
        currentRow++;

        worksheet.Cell(currentRow, 1).SetValue("Work Days:");
        worksheet.Cell(currentRow, 2).SetValue(report.TotalWorkDays);
        currentRow++;

        worksheet.Cell(currentRow, 1).SetValue("Present:");
        worksheet.Cell(currentRow, 2).SetValue(report.PresentDays);
        currentRow++;

        worksheet.Cell(currentRow, 1).SetValue("Late:");
        worksheet.Cell(currentRow, 2).SetValue(report.LateDays);
        currentRow++;

        worksheet.Cell(currentRow, 1).SetValue("Absent:");
        worksheet.Cell(currentRow, 2).SetValue(report.AbsentDays);
        currentRow++;

        worksheet.Cell(currentRow, 1).SetValue("Overtime Hours:");
        worksheet.Cell(currentRow, 2).SetValue(report.OvertimeHours);
        currentRow++;

        currentRow++;

        worksheet.Cell(currentRow, 1).SetValue("Undertime Hours:");
        worksheet.Cell(currentRow, 2).SetValue(report.UndertimeHours);
        currentRow++;

        worksheet.Cell(currentRow, 1).SetValue("Leave Days:");
        worksheet.Cell(currentRow, 2).SetValue(report.LeaveDays);
        currentRow++;

        worksheet.Cell(currentRow, 1).SetValue("Leave Balance:");
        worksheet.Cell(currentRow, 2).SetValue(report.LeaveBalance);
        currentRow++;

        worksheet.Cell(currentRow, 1).SetValue("Attendance Rate:");
        worksheet.Cell(currentRow, 2).SetValue($"{report.AttendanceRate:F1}%");
        currentRow++;

        worksheet.Cell(currentRow, 1).SetValue("Punctuality Rate:");
        worksheet.Cell(currentRow, 2).SetValue($"{report.PunctualityRate:F1}%");
        currentRow += 2;

        // Average times
        worksheet.Cell(currentRow, 1).SetValue("Average Times");
        worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
        worksheet.Cell(currentRow, 1).Style.Font.FontSize = 12;
        currentRow++;

        worksheet.Cell(currentRow, 1).SetValue("Average Check-in:");
        worksheet.Cell(currentRow, 2).SetValue(report.AvgCheckIn ?? "N/A");
        currentRow++;

        worksheet.Cell(currentRow, 1).SetValue("Average Check-out:");
        worksheet.Cell(currentRow, 2).SetValue(report.AvgCheckOut ?? "N/A");
        currentRow++;

        worksheet.Cell(currentRow, 1).SetValue("Max Late Minutes:");
        worksheet.Cell(currentRow, 2).SetValue(report.MaxLateMinutes);
        currentRow++;

        worksheet.Cell(currentRow, 1).SetValue("Consecutive Present Days:");
        worksheet.Cell(currentRow, 2).SetValue(report.ConsecutivePresentDays);
        currentRow += 2;

        // Attendance Records
        if (report.AttendanceRecords.Any())
        {
            worksheet.Cell(currentRow, 1).SetValue("Attendance Records");
            worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
            worksheet.Cell(currentRow, 1).Style.Font.FontSize = 12;
            currentRow++;

            // Headers
            var headers = new[] { "Date", "Check In", "Check Out", "Worked Hours", "Overtime Hours", "Undertime Hours", "Status", "Note" };
            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(currentRow, i + 1).SetValue(headers[i]);
                worksheet.Cell(currentRow, i + 1).Style = headerStyle;
            }
            currentRow++;

            // Data rows
            foreach (var record in report.AttendanceRecords)
            {
                worksheet.Cell(currentRow, 1).SetValue($"{record.Date:yyyy-MM-dd}");
                worksheet.Cell(currentRow, 2).SetValue(record.CheckIn?.ToString(@"HH\:mm") ?? "");
                worksheet.Cell(currentRow, 3).SetValue(record.CheckOut?.ToString(@"HH\:mm") ?? "");
                worksheet.Cell(currentRow, 4).SetValue(record.WorkedHours?.ToString("F1") ?? "");
                worksheet.Cell(currentRow, 5).SetValue(record.OvertimeHours?.ToString("F1") ?? "");
                worksheet.Cell(currentRow, 6).SetValue(record.UndertimeHours?.ToString("F1") ?? "");
                worksheet.Cell(currentRow, 7).SetValue(record.Status);
                worksheet.Cell(currentRow, 8).SetValue(record.Note ?? "");

                // Apply data style to all cells in the row
                for (int i = 1; i <= 8; i++)
                {
                    worksheet.Cell(currentRow, i).Style = dataStyle;
                }
                currentRow++;
            }
        }
        else
        {
            worksheet.Cell(currentRow, 1).SetValue("No attendance records found for this period.");
            worksheet.Range(worksheet.Cell(currentRow, 1), worksheet.Cell(currentRow, 8)).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.Italic = true;
            worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            currentRow++;
        }

        currentRow += 2;

        // Leave Requests
        if (report.LeaveRequests.Any())
        {
            worksheet.Cell(currentRow, 1).SetValue("Leave Requests");
            worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
            worksheet.Cell(currentRow, 1).Style.Font.FontSize = 12;
            currentRow++;

            // Headers
            var leaveHeaders = new[] { "Requested At", "Leave Type", "Start Date", "End Date", "Days", "Reason", "Status" };
            for (int i = 0; i < leaveHeaders.Length; i++)
            {
                worksheet.Cell(currentRow, i + 1).SetValue(leaveHeaders[i]);
                worksheet.Cell(currentRow, i + 1).Style = headerStyle;
            }
            currentRow++;

            // Data rows
            foreach (var leave in report.LeaveRequests)
            {
                worksheet.Cell(currentRow, 1).SetValue($"{leave.RequestedAt:yyyy-MM-dd HH:mm}");
                worksheet.Cell(currentRow, 2).SetValue(leave.LeaveType);
                worksheet.Cell(currentRow, 3).SetValue($"{leave.StartDate:yyyy-MM-dd}");
                worksheet.Cell(currentRow, 4).SetValue($"{leave.EndDate:yyyy-MM-dd}");
                worksheet.Cell(currentRow, 5).SetValue(leave.Days.ToString());
                worksheet.Cell(currentRow, 6).SetValue(leave.Reason ?? "");
                worksheet.Cell(currentRow, 7).SetValue(leave.Status);

                // Apply data style to all cells in the row
                for (int i = 1; i <= 7; i++)
                {
                    worksheet.Cell(currentRow, i).Style = dataStyle;
                }
                currentRow++;
            }
        }
        else
        {
            worksheet.Cell(currentRow, 1).SetValue("No leave requests found for this period.");
            worksheet.Range(worksheet.Cell(currentRow, 1), worksheet.Cell(currentRow, 7)).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.Italic = true;
            worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            currentRow++;
        }

        currentRow += 2;

        // Leave Balances
        if (report.LeaveBalances.Any())
        {
            worksheet.Cell(currentRow, 1).SetValue("Leave Balances");
            worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
            worksheet.Cell(currentRow, 1).Style.Font.FontSize = 12;
            currentRow++;

            // Headers
            var balanceHeaders = new[] { "Leave Type", "Total", "Used", "Remaining" };
            for (int i = 0; i < balanceHeaders.Length; i++)
            {
                worksheet.Cell(currentRow, i + 1).SetValue(balanceHeaders[i]);
                worksheet.Cell(currentRow, i + 1).Style = headerStyle;
            }
            currentRow++;

            // Data rows
            foreach (var balance in report.LeaveBalances)
            {
                worksheet.Cell(currentRow, 1).SetValue(balance.LeaveType);
                worksheet.Cell(currentRow, 2).SetValue(balance.Total.ToString());
                worksheet.Cell(currentRow, 3).SetValue(balance.Used.ToString());
                worksheet.Cell(currentRow, 4).SetValue(balance.Remaining.ToString());

                // Apply data style to all cells in the row
                for (int i = 1; i <= 4; i++)
                {
                    worksheet.Cell(currentRow, i).Style = dataStyle;
                }
                currentRow++;
            }
        }
        else
        {
            worksheet.Cell(currentRow, 1).SetValue("No leave balances found.");
            worksheet.Range(worksheet.Cell(currentRow, 1), worksheet.Cell(currentRow, 4)).Merge();
            worksheet.Cell(currentRow, 1).Style.Font.Italic = true;
            worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            currentRow++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return memoryStream.ToArray();
    }

    private static IXLCell GetCell(IXLWorksheet worksheet, int row, int column)
    {
        return worksheet.Cell(row, column);
    }
}
