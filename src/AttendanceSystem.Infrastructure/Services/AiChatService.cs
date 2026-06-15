using AttendanceSystem.Application.DTOs.AI;
using AttendanceSystem.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using AttendanceSystem.Infrastructure.Persistence;
using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Infrastructure.Services;

public class AiChatService : IAiChatService
{
    private readonly IAiProvider _aiProvider;
    private readonly ApplicationDbContext _dbContext;

    public AiChatService(IAiProvider aiProvider, ApplicationDbContext dbContext)
    {
        _aiProvider = aiProvider;
        _dbContext = dbContext;
    }

    public async Task<ChatResponseDto> GetAdminResponseAsync(
        ChatRequestDto request, Guid adminId, CancellationToken cancellationToken = default)
    {
        var context = await BuildAdminContextAsync(cancellationToken);

        var systemPrompt =
            $"Та HR удирдлагын системийн AI туслах. Монгол хэлээр хариул.\n\n" +
            $"БОДИТ ӨГӨГДӨЛ (өнөөдөр {DateTime.UtcNow:yyyy-MM-dd}):\n" +
            $"- Нийт ажилтан: {context.TotalEmployees}\n" +
            $"- Өнөөдөр ирсэн: {context.PresentToday}\n" +
            $"- Өнөөдөр ирээгүй: {context.AbsentToday}\n" +
            $"- Чөлөөтэй: {context.OnLeaveToday}\n" +
            $"- Хүлээгдэж буй амралтын хүсэлт: {context.PendingLeaveRequests}\n" +
            $"- Хэлтэсүүд: {string.Join(", ", context.DepartmentNames)}\n\n" +
            $"ДҮРЭМ:\n" +
            $"- Дээрх бодит өгөгдөлд үндэслэн хариул\n" +
            $"- Товч, тодорхой, мэргэжлийн өнгө аястай байх\n" +
            $"- 'HR рүү хандаарай' гэх ерөнхий хариулт өгөхгүй";

        var messages = BuildMessageHistory(request);
        var reply = await _aiProvider.GenerateReplyAsync(systemPrompt, messages, cancellationToken);
        return new ChatResponseDto { Reply = reply, Timestamp = DateTime.UtcNow };
    }

    public async Task<ChatResponseDto> GetEmployeeResponseAsync(
        ChatRequestDto request, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var context = await BuildEmployeeContextAsync(employeeId, cancellationToken);

        var leaveHistory = context.RecentLeaves.Any()
            ? string.Join("\n", context.RecentLeaves.Select(l =>
                $"  • {l.StartDate} ~ {l.EndDate} ({l.Days} хоног) [{l.Status}] - {l.Reason}"))
            : "  • Амралтын түүх байхгүй";

        var attendanceHistory = context.RecentAttendance.Any()
            ? string.Join("\n", context.RecentAttendance.Select(a =>
                $"  • {a.Date}: {a.Status} ирсэн:{a.CheckIn ?? "-"} явсан:{a.CheckOut ?? "-"}"))
            : "  • Ирцийн бүртгэл байхгүй";

        var systemPrompt =
            $"Та HR системийн AI туслах. Монгол хэлээр хариул.\n\n" +
            $"АЖИЛТАНЫ БОДИТ МЭДЭЭЛЭЛ:\n" +
            $"- Нэр: {context.EmployeeName}\n" +
            $"- Хэлтэс: {context.Department}\n" +
            $"- Өнөөдөр ирц бүртгэсэн: {(context.IsCheckedInToday ? "Тийм" : "Үгүй")}\n" +
            $"- Сүүлийн ирсэн цаг: {(context.LastCheckIn.HasValue ? context.LastCheckIn.Value.ToString("yyyy-MM-dd HH:mm") : "Байхгүй")}\n" +
            $"- Сүүлийн явсан цаг: {(context.LastCheckOut.HasValue ? context.LastCheckOut.Value.ToString("yyyy-MM-dd HH:mm") : "Байхгүй")}\n" +
            $"- Энэ сард хоцорсон тоо: {context.LateCountThisMonth} удаа\n\n" +
            $"АМРАЛТЫН МЭДЭЭЛЭЛ:\n" +
            $"- Жилийн нийт амралт: {context.TotalAnnualLeave} хоног\n" +
            $"- Ашигласан: {context.UsedLeaveDays} хоног\n" +
            $"- Үлдсэн: {context.LeaveBalance} хоног\n" +
            $"- Сүүлийн амралтууд:\n{leaveHistory}\n\n" +
            $"СҮҮЛИЙН 7 ХОНОГИЙН ИРЦ:\n{attendanceHistory}\n\n" +
            $"ДҮРЭМ:\n" +
            $"- ЗААВАЛ дээрх бодит өгөгдөлд үндэслэн хариул\n" +
            $"- 'Системд нэвтрэх', 'HR рүү хандах' гэх ерөнхий хариулт өгөхгүй\n" +
            $"- Тоо, огноог яг тодорхой хэлэх";

        var messages = BuildMessageHistory(request);
        var reply = await _aiProvider.GenerateReplyAsync(systemPrompt, messages, cancellationToken);
        return new ChatResponseDto { Reply = reply, Timestamp = DateTime.UtcNow };
    }

    private static List<(string Role, string Content)> BuildMessageHistory(ChatRequestDto request)
    {
        var messages = new List<(string Role, string Content)>();
        if (request.History != null)
            foreach (var msg in request.History)
                messages.Add((msg.Role == "assistant" ? "assistant" : "user", msg.Content));
        messages.Add(("user", request.Message));
        return messages;
    }

    private async Task<AdminAiContextDto> BuildAdminContextAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var totalEmployees = await _dbContext.Employees.CountAsync(cancellationToken);
        var presentToday = await _dbContext.AttendanceRecords
            .Where(a => a.Date == today && a.Status == AttendanceStatus.Present)
            .Select(a => a.EmployeeId).Distinct().CountAsync(cancellationToken);
        var onLeaveToday = await _dbContext.LeaveRequests
            .Where(l => l.Status == RequestStatus.Approved && l.StartDate <= today && l.EndDate >= today)
            .Select(l => l.EmployeeId).Distinct().CountAsync(cancellationToken);
        var pendingLeaveRequests = await _dbContext.LeaveRequests
            .CountAsync(l => l.Status == RequestStatus.Pending, cancellationToken);
        var departmentNames = await _dbContext.Departments
            .Select(d => d.Name).ToListAsync(cancellationToken);

        return new AdminAiContextDto
        {
            TotalEmployees = totalEmployees,
            PresentToday = presentToday,
            AbsentToday = Math.Max(totalEmployees - presentToday - onLeaveToday, 0),
            OnLeaveToday = onLeaveToday,
            PendingLeaveRequests = pendingLeaveRequests,
            DepartmentNames = departmentNames
        };
    }

    private async Task<EmployeeAiContextDto> BuildEmployeeContextAsync(
        Guid employeeId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);
        var sevenDaysAgo = today.AddDays(-7);

        var employee = await _dbContext.Employees
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);

        var todayRecord = await _dbContext.AttendanceRecords
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == today, cancellationToken);

        var lastRecord = await _dbContext.AttendanceRecords
            .Where(a => a.EmployeeId == employeeId)
            .OrderByDescending(a => a.Date)
            .FirstOrDefaultAsync(cancellationToken);

        // ✅ SumAsync алдааг засах — ToList хийгээд тооцоолно
        var approvedLeaves = await _dbContext.LeaveRequests
            .Where(l => l.EmployeeId == employeeId && l.Status == RequestStatus.Approved)
            .Select(l => new { l.StartDate, l.EndDate, l.Reason })
            .ToListAsync(cancellationToken);

        var usedLeaveDays = approvedLeaves
            .Sum(l => l.EndDate.DayNumber - l.StartDate.DayNumber + 1);

        // Сүүлийн 5 амралт
        var recentLeaves = await _dbContext.LeaveRequests
            .Where(l => l.EmployeeId == employeeId)
            .OrderByDescending(l => l.StartDate)
            .Take(5)
            .ToListAsync(cancellationToken);

        // Сүүлийн 7 хоногийн ирц
        var recentAttendance = await _dbContext.AttendanceRecords
            .Where(a => a.EmployeeId == employeeId && a.Date >= sevenDaysAgo)
            .OrderByDescending(a => a.Date)
            .ToListAsync(cancellationToken);

        // Энэ сард хоцорсон тоо
        var lateCount = await _dbContext.AttendanceRecords
            .CountAsync(a => a.EmployeeId == employeeId &&
                             a.Date >= firstOfMonth &&
                             a.Status == AttendanceStatus.Late, cancellationToken);

        const int annualLeave = 15;

        return new EmployeeAiContextDto
        {
            EmployeeName = employee?.FullName ?? "Тодорхойгүй",
            Department = employee?.Department?.Name ?? "Тодорхойгүй",
            UsedLeaveDays = usedLeaveDays,
            TotalAnnualLeave = annualLeave,
            LeaveBalance = Math.Max(annualLeave - usedLeaveDays, 0),
            IsCheckedInToday = todayRecord?.CheckInTime != null,
            LastCheckIn = todayRecord?.CheckInTime,
            LastCheckOut = lastRecord?.CheckOutTime,
            LateCountThisMonth = lateCount,
            RecentLeaves = recentLeaves.Select(l => new LeaveHistoryItem
            {
                StartDate = l.StartDate.ToString("yyyy-MM-dd"),
                EndDate = l.EndDate.ToString("yyyy-MM-dd"),
                Days = (int)l.TotalDays,
                Status = l.Status.ToString(),
                Reason = l.Reason ?? "-"
            }).ToList(),
            RecentAttendance = recentAttendance.Select(a => new AttendanceHistoryItem
            {
                Date = a.Date.ToString("yyyy-MM-dd"),
                Status = a.Status.ToString(),
                CheckIn = a.CheckInTime.ToString("HH:mm"),
                CheckOut = a.CheckOutTime?.ToString("HH:mm")
            }).ToList()
        };
    }
}