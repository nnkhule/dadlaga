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
            $"Та HR удирдлагын системийн AI туслах. Админ хэрэглэгчид ирц, ажилтан, амралтын " +
            $"мэдээллийг тайлбарлаж, асуултад товч бөгөөд тодорхой хариулт өгнө.\n\n" +
            $"Одоогийн систем мэдээлэл:\n" +
            $"- Нийт ажилтан: {context.TotalEmployees}\n" +
            $"- Өнөөдөр ирсэн: {context.PresentToday}\n" +
            $"- Өнөөдөр ирээгүй: {context.AbsentToday}\n" +
            $"- Чөлөөтэй: {context.OnLeaveToday}\n" +
            $"- Хүлээгдэж буй амралтын хүсэлт: {context.PendingLeaveRequests}\n" +
            $"- Хэлтэсүүд: {string.Join(", ", context.DepartmentNames)}\n\n" +
            $"Хариултаа монгол хэлээр, найрсаг, мэргэжлийн өнгө аястай өг.";

        var messages = BuildMessageHistory(request);
        var reply = await _aiProvider.GenerateReplyAsync(systemPrompt, messages, cancellationToken);

        return new ChatResponseDto
        {
            Reply = reply,
            Timestamp = DateTime.UtcNow
        };
    }

    public async Task<ChatResponseDto> GetEmployeeResponseAsync(
        ChatRequestDto request, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var context = await BuildEmployeeContextAsync(employeeId, cancellationToken);

        var systemPrompt =
            $"Та HR системийн AI туслах. Ажилтан '{context.EmployeeName}' ({context.Department} хэлтэс) -тэй ярьж байна.\n\n" +
            $"Ажилтаны мэдээлэл:\n" +
            $"- Үлдсэн амралтын хоног: {context.LeaveBalance}\n" +
            $"- Өнөөдөр ирц бүртгэсэн эсэх: {(context.IsCheckedInToday ? "Тийм" : "Үгүй")}\n" +
            $"- Сүүлийн ирсэн цаг: {(context.LastCheckIn.HasValue ? context.LastCheckIn.Value.ToString("yyyy-MM-dd HH:mm") : "Байхгүй")}\n" +
            $"- Сүүлийн явсан цаг: {(context.LastCheckOut.HasValue ? context.LastCheckOut.Value.ToString("yyyy-MM-dd HH:mm") : "Байхгүй")}\n\n" +
            $"Хариултаа монгол хэлээр, найрсаг, тодорхой өг. Ирц, амралт, цалин зэрэг асуултад дээрх мэдээллийг ашиглан хариул.";

        var messages = BuildMessageHistory(request);
        var reply = await _aiProvider.GenerateReplyAsync(systemPrompt, messages, cancellationToken);

        return new ChatResponseDto
        {
            Reply = reply,
            Timestamp = DateTime.UtcNow
        };
    }

    private static List<(string Role, string Content)> BuildMessageHistory(ChatRequestDto request)
    {
        var messages = new List<(string Role, string Content)>();

        if (request.History != null)
        {
            foreach (var msg in request.History)
            {
                var role = msg.Role == "assistant" ? "assistant" : "user";
                messages.Add((role, msg.Content));
            }
        }

        messages.Add(("user", request.Message));
        return messages;
    }

    private async Task<AdminAiContextDto> BuildAdminContextAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var totalEmployees = await _dbContext.Employees.CountAsync(cancellationToken);

        var presentToday = await _dbContext.AttendanceRecords
            .Where(a => a.Date == today && a.Status == AttendanceStatus.Present)
            .Select(a => a.EmployeeId)
            .Distinct()
            .CountAsync(cancellationToken);

        var onLeaveToday = await _dbContext.LeaveRequests
            .Where(l => l.Status == RequestStatus.Approved &&
                        l.StartDate <= today && l.EndDate >= today)
            .Select(l => l.EmployeeId)
            .Distinct()
            .CountAsync(cancellationToken);

        var pendingLeaveRequests = await _dbContext.LeaveRequests
            .CountAsync(l => l.Status == RequestStatus.Pending, cancellationToken);

        var departmentNames = await _dbContext.Departments
            .Select(d => d.Name)
            .ToListAsync(cancellationToken);

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

    private async Task<EmployeeAiContextDto> BuildEmployeeContextAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await _dbContext.Employees
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var todayRecord = await _dbContext.AttendanceRecords
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == today, cancellationToken);

        var lastRecord = await _dbContext.AttendanceRecords
            .Where(a => a.EmployeeId == employeeId)
            .OrderByDescending(a => a.Date)
            .FirstOrDefaultAsync(cancellationToken);

        var usedLeaveDays = await _dbContext.LeaveRequests
            .Where(l => l.EmployeeId == employeeId && l.Status == RequestStatus.Approved)
            .SumAsync(l => (l.EndDate.DayNumber - l.StartDate.DayNumber + 1), cancellationToken);

        const int annualLeaveAllowance = 15;

        return new EmployeeAiContextDto
        {
            EmployeeName = employee?.FullName ?? "Тодорхойгүй",
            Department = employee?.Department?.Name ?? "Тодорхойгүй",
            LeaveBalance = Math.Max(annualLeaveAllowance - usedLeaveDays, 0),
            IsCheckedInToday = todayRecord?.CheckInTime != null,
            LastCheckIn = todayRecord?.CheckInTime,
            LastCheckOut = lastRecord?.CheckOutTime
        };
    }
}