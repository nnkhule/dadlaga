using AttendanceSystem.Application.DTOs.AI;
using AttendanceSystem.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using AttendanceSystem.Infrastructure.Persistence;
using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Infrastructure.Services;

public class AiChatService : IAiChatService
{
    private const int MaxHistoryMessages = 120; 

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
        var systemPrompt = BuildAdminSystemPrompt(context);
        var messages = BuildMessageHistory(request);
        var reply = await _aiProvider.GenerateReplyAsync(systemPrompt, messages, cancellationToken);
        return new ChatResponseDto { Reply = reply, Timestamp = DateTime.UtcNow };
    }

    public async Task<ChatResponseDto> GetEmployeeResponseAsync(
        ChatRequestDto request, Guid employeeId, CancellationToken cancellationToken = default)
    {
        var context = await BuildEmployeeContextAsync(employeeId, cancellationToken);
        var systemPrompt = BuildEmployeeSystemPrompt(context);
        var messages = BuildMessageHistory(request);
        var reply = await _aiProvider.GenerateReplyAsync(systemPrompt, messages, cancellationToken);
        return new ChatResponseDto { Reply = reply, Timestamp = DateTime.UtcNow };
    }

    // ──────────────────────────────────────────────────────────
    //  PROMPT BUILDERS
    // ──────────────────────────────────────────────────────────

    private static string BuildAdminSystemPrompt(AdminAiContextDto ctx)
    {
        var deptBreakdown = ctx.DepartmentBreakdown.Count > 0
            ? string.Join("\n", ctx.DepartmentBreakdown.Select(d =>
                $"  • {d.DepartmentName}: {d.PresentToday}/{d.EmployeeCount} ирсэн ({d.AttendanceRate:F0}%)"))
            : "  • Дата байхгүй";

        var trend = ctx.Last7DaysTrend.Count > 0
            ? string.Join("\n", ctx.Last7DaysTrend.Select(t =>
                $"  • {t.Date}: Ирсэн {t.Present}, Хоцорсон {t.Late}, Ирээгүй {t.Absent}"))
            : "  • Дата байхгүй";

        var topLate = ctx.TopLateEmployeesThisMonth.Count > 0
            ? string.Join("\n", ctx.TopLateEmployeesThisMonth.Select(e =>
                $"  • {e.EmployeeName}: {e.LateCount} удаа хоцорсон (нийт {e.TotalLateMinutes:F0} мин)"))
            : "  • Энэ сард хоцролт бүртгэгдээгүй";

        return $"""
            Та "AttendIQ" компанийн дотоод AI туслах. Зөвхөн админ, HR-менежерүүдтэй ажиллаж байна.
            Хариултаа МОНГОЛ хэлээр, тодорхой, мэргэжлийн өнгө аястай бичнэ.

            ТАНЫ ХАРИУЦАХ АСУУДЛЫН ХҮРЭЭ (үүний гадна асуултад хариулахгүй):
            1. Системийн бодит дата — ирц, амралт, хэлтэс, ажилтны статистик (доор өгөгдсөн).
            2. HR бодлого, дүрэм журам — ирцийн дүрэм, амралтын журам, хоцролт зохицуулалт зэрэг компанийн дотоод асуудал.
            3. Хөдөлмөрийн эрх зүй — Монгол улсын Хөдөлмөрийн тухай хууль, амралт/илүү цаг/халах журмын ерөнхий зарчим (хуулийн зөвлөгөө биш, ерөнхий мэдээлэл гэдгийг анхааруул).
            4. Цалин, илүү цаг, амралтын тооцоолол — томьёо, жишээ тооцоолол хийх (жишээ нь илүү цагийн хөнгөлөлт хэрхэн тооцох вэ).
            5. Багийн удирдлага, ажилтны гүйцэтгэл сайжруулах зөвлөгөө (ирцийн дата дээр үндэслэсэн).

            ХҮРЭЭНИЙ ГАДНА АСУУЛТ ИРВЭЛ (компанитай хамаагүй ерөнхий мэдлэг, зугаа цэнгэл, хувийн зөвлөгөө гэх мэт):
            - Эелдэгээр "Энэ асуулт миний хариуцдаг ирц/HR-ийн хүрээнээс гадуур байна" гэж тайлбарлаад, тухайн зорилгод тохирох ерөнхий AI ашиглахыг санал болго.

            === ӨНӨӨДРИЙН ТОЙМ ({DateTime.UtcNow:yyyy-MM-dd}) ===
            • Нийт ажилтан: {ctx.TotalEmployees}
            • Өнөөдөр ирсэн: {ctx.PresentToday + ctx.LateTodayCount}
            • Өнөөдөр хоцорсон: {ctx.LateTodayCount}
            • Өнөөдөр ирээгүй: {ctx.AbsentToday}
            • Чөлөөтэй: {ctx.OnLeaveToday}
            • Хүлээгдэж буй амралтын хүсэлт: {ctx.PendingLeaveRequests}
            • Хэлтэсүүд: {string.Join(", ", ctx.DepartmentNames)}

            === ЭНЭ САРЫН ТОЙМ ===
            • Дундаж ирцийн хувь: {ctx.AttendanceRateThisMonth:F1}%
            • Дундаж хоцролт: {ctx.AvgLateMinutesThisMonth:F0} минут/удаа
            • Нийт илүү цаг: {ctx.TotalOvertimeHoursThisMonth:F1} цаг
            • Сэжигтэй бүртгэл (сүүлийн 7 хоног): {ctx.SuspiciousRecordsThisWeek}

            === ХЭЛТСИЙН ХУВААРИЛАЛТ (өнөөдөр) ===
            {deptBreakdown}

            === СҮҮЛИЙН 7 ХОНОГИЙН ЧИГ ХАНДЛАГА ===
            {trend}

            === ЭНЭ САРД ХАМГИЙН ИХ ХОЦОРСОН АЖИЛТНУУД ===
            {topLate}

            ЗААВАР:
            - Системийн дата талаар асуувал ЗӨВХӨН дээрх бодит тоо дээр тулгуурлан хариул, зохиож хэлэхгүй.
            - HR бодлого/хууль/тооцооллын асуултад мэргэжлийн мэдлэгээ ашиглан тодорхой, ойлгомжтой хариулт өг — жишээ тооцоолол хэрэгтэй бол тоон жишээгээр харуул.
            - Хөдөлмөрийн хуулийн талаар ярихдаа "энэ бол ерөнхий мэдээлэл, хуулийн нарийвчилсан зөвлөгөөг хуульчаас авна уу" гэж сануулна.
            - Хэрэв системийн асуултад хариулах дата чамд байхгүй бол үнэнээр "энэ мэдээлэл одоогийн context-д байхгүй" гэж хэлж, ямар тайлан харж болохыг санал болго.
            - Ажилтны хувийн нууцлалтай мэдээлэл (тодорхой хүний цалин, эрүүл мэндийн дэлгэрэнгүй) шаардсан асуултад "энэ мэдээллийг шууд харах эрх надад байхгүй" гэж тайлбарла.
            - Хариултыг товч, бүтэцлэгдсэн хэлбэрээр бич — урт тайлбараас зайлсхий.
            """;
    }

    private static string BuildEmployeeSystemPrompt(EmployeeAiContextDto ctx)
    {
        var leaveHistory = ctx.RecentLeaves.Count > 0
            ? string.Join("\n", ctx.RecentLeaves.Select(l =>
                $"  • {l.StartDate} ~ {l.EndDate} ({l.Days} хоног) [{TranslateStatus(l.Status)}] — {l.Reason}"))
            : "  • Амралтын түүх байхгүй";

        var attendanceHistory = ctx.RecentAttendance.Count > 0
            ? string.Join("\n", ctx.RecentAttendance.Select(a =>
                $"  • {a.Date}: {TranslateAttendanceStatus(a.Status)} (ирсэн:{a.CheckIn ?? "-"} явсан:{a.CheckOut ?? "-"})"))
            : "  • Ирцийн бүртгэл байхгүй";

        var upcomingLeave = ctx.UpcomingApprovedLeave.HasValue
            ? $"{ctx.UpcomingApprovedLeave.Value:yyyy-MM-dd}-ээс эхлэх батлагдсан амралт бий"
            : "Удахгүй эхлэх батлагдсан амралт байхгүй";

        var trendText = ctx.LateTrend switch
        {
            "improving" => "сайжирч байна (өмнөх долоо хоногтой харьцуулахад хоцролт буурсан)",
            "worsening" => "муудаж байна (өмнөх долоо хоногтой харьцуулахад хоцролт нэмэгдсэн)",
            _ => "тогтвортой байна"
        };

        return $"""
            Та "AttendIQ" компанийн ирцийн системийн хувийн туслах AI. Зөвхөн нэг ажилтантай ярьж байна.
            Хариултаа МОНГОЛ хэлээр, дотно, дэмжих өнгө аястай, гэхдээ мэргэжлийн түвшинд бичнэ.

            ТАНЫ ХАРИУЦАХ АСУУДЛЫН ХҮРЭЭ (үүний гадна асуултад хариулахгүй):
            1. Ажилтны өөрийн ирц, амралт, гүйцэтгэлийн бодит дата (доор өгөгдсөн).
            2. Компанийн HR бодлого, журам — амралт авах дараалал, хоцролтын дүрэм, илүү цагийн зохицуулалт зэрэг.
            3. Хөдөлмөрийн эрх зүй — ажилтны эрх (амралт, илүү цаг, чөлөөний хоног) талаарх ерөнхий мэдээлэл (хуулийн зөвлөгөө биш гэдгийг анхааруул).
            4. Цалин, илүү цагийн тооцоолол — ерөнхий томьёо, жишээ тооцоолол.

            ХҮРЭЭНИЙ ГАДНА АСУУЛТ ИРВЭЛ (компанитай хамаагүй ерөнхий мэдлэг, зугаа цэнгэл, хувийн зөвлөгөө гэх мэт):
            - Эелдэгээр "Энэ асуулт миний хариуцдаг ирц/HR-ийн хүрээнээс гадуур байна" гэж тайлбарлаад, тухайн зорилгод тохирох ерөнхий AI ашиглахыг санал болго.

            === АЖИЛТНЫ МЭДЭЭЛЭЛ ===
            • Нэр: {ctx.EmployeeName}
            • Хэлтэс: {ctx.Department}
            • Ажилласан хугацаа: {ctx.TenureMonths} сар
            • Өнөөдөр ирц бүртгүүлсэн: {(ctx.IsCheckedInToday ? "Тийм" : "Үгүй")}
            • Сүүлийн ирсэн цаг: {(ctx.LastCheckIn.HasValue ? ctx.LastCheckIn.Value.ToString("yyyy-MM-dd HH:mm") : "Байхгүй")}
            • Сүүлийн явсан цаг: {(ctx.LastCheckOut.HasValue ? ctx.LastCheckOut.Value.ToString("yyyy-MM-dd HH:mm") : "Байхгүй")}
            • Энэ сард хоцорсон тоо: {ctx.LateCountThisMonth} удаа (дундаж {ctx.AvgLateMinutesThisMonth:F0} мин)
            • Энэ сард ирээгүй өдөр: {ctx.AbsentCountThisMonth}
            • Энэ сарын илүү цаг: {ctx.OvertimeHoursThisMonth:F1} цаг
            • Хоцролтын чиг хандлага: {trendText}
            • {upcomingLeave}

            === АМРАЛТЫН МЭДЭЭЛЭЛ ===
            • Жилийн нийт амралт: {ctx.TotalAnnualLeave} хоног
            • Ашигласан: {ctx.UsedLeaveDays} хоног
            • Үлдсэн: {ctx.LeaveBalance} хоног
            • Сүүлийн амралтууд:
            {leaveHistory}

            === СҮҮЛИЙН 7 ХОНОГИЙН ИРЦ ===
            {attendanceHistory}

            ЗААВАР:
            - Хувийн дата талаар асуувал ЗӨВХӨН дээрх бодит мэдээлэл дээр тулгуурлан хариул, өөрөөсөө тоо зохиож хэлэхгүй.
            - HR бодлого/хуулийн ерөнхий асуултад мэдлэгээ ашиглан тодорхой хариулт өг, гэхдээ "энэ ерөнхий мэдээлэл, тодорхой тохиолдолд HR-тэй холбогдоорой" гэж сануулна.
            - "Системд нэвтрэх" гэх ерөнхий хариулт өгөхөөс зайлсхий — өгөгдсөн дата дээрээ үндэслэн шууд хариулт өг.
            - Хэрэв хувийн дата шаардсан асуултад хариулах мэдээлэл байхгүй бол "энэ мэдээлэл одоогийн context-д байхгүй, HR-тэй холбогдоно уу" гэж шударгаар хэлэх.
            - Хоцролт/ирц муу байгаа бол шийтгэлийн өнгөөргүй, дэмжсэн зөвлөгөө өг (жишээ нь эрт босох, тээврийн төлөвлөгөө гэх мэт).
            - Цалин, бусад ажилтны хувийн мэдээлэл асуувал хариулахгүй, HR-тэй шууд холбогдохыг зөвлө.
            """;
    }

    private static string TranslateStatus(string status) => status switch
    {
        "Approved" => "Зөвшөөрсөн",
        "Pending"  => "Хүлээгдэж буй",
        "Rejected" => "Татгалзсан",
        _ => status
    };

    private static string TranslateAttendanceStatus(string status) => status switch
    {
        "Present"             => "Ирсэн",
        "Late"                => "Хоцорсон",
        "EarlyLeave"          => "Эрт явсан",
        "Absent"              => "Ирээгүй",
        "OnLeave"             => "Чөлөөтэй",
        "Holiday"             => "Амралтын өдөр",
        "NightShift"          => "Шөнийн ажил",
        "WeekendWork"         => "Амралтын өдрийн ажил",
        "HalfDay"             => "Хагас өдөр",
        "PendingManualReview" => "Шалгагдаж буй",
        _ => status
    };


    private static List<(string Role, string Content)> BuildMessageHistory(ChatRequestDto request)
    {
        var messages = new List<(string Role, string Content)>();

        if (request.History is { Count: > 0 })
        {
            var trimmed = request.History
                .Skip(Math.Max(0, request.History.Count - MaxHistoryMessages))
                .ToList();

            foreach (var msg in trimmed)
                messages.Add((msg.Role == "assistant" ? "assistant" : "user", msg.Content));
        }

        messages.Add(("user", request.Message));
        return messages;
    }

    // ──────────────────────────────────────────────────────────
    //  CONTEXT BUILDERS
    // ──────────────────────────────────────────────────────────

    private async Task<AdminAiContextDto> BuildAdminContextAsync(CancellationToken cancellationToken)
    {
        var today        = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);
        var sevenDaysAgo = today.AddDays(-6);

        var totalEmployees = await _dbContext.Employees.CountAsync(e => e.IsActive, cancellationToken);

        var presentTodayRecords = await _dbContext.AttendanceRecords
            .Where(a => a.Date == today)
            .Select(a => new { a.EmployeeId, a.Status })
            .Distinct()
            .ToListAsync(cancellationToken);
        var presentTodayCount = presentTodayRecords.Count(a => AttendanceStatusClassifier.IsPresentBucket(a.Status));

        var lateToday = await _dbContext.AttendanceRecords
            .Where(a => a.Date == today && a.Status == AttendanceStatus.Late)
            .Select(a => a.EmployeeId).Distinct().CountAsync(cancellationToken);

        var onLeaveToday = await _dbContext.LeaveRequests
            .Where(l => l.Status == RequestStatus.Approved && l.StartDate <= today && l.EndDate >= today)
            .Select(l => l.EmployeeId).Distinct().CountAsync(cancellationToken);

        var pendingLeaveRequests = await _dbContext.LeaveRequests
            .CountAsync(l => l.Status == RequestStatus.Pending, cancellationToken);

        var departments = await _dbContext.Departments
            .Select(d => new { d.Id, d.Name })
            .ToListAsync(cancellationToken);

        // Энэ сарын ирцийн статистик (raw → in-memory тооцоолол)
        var monthRecords = await _dbContext.AttendanceRecords
            .Where(a => a.Date >= firstOfMonth && a.Date <= today)
            .Select(a => new { a.Status, a.LateMinutes, a.OvertimeHours, a.IsSuspicious, a.Date, a.EmployeeId })
            .ToListAsync(cancellationToken);

        var attendanceRateThisMonth = monthRecords.Count > 0
            ? (decimal)monthRecords.Count(r => r.Status is AttendanceStatus.Present or AttendanceStatus.Late
                                                or AttendanceStatus.HalfDay or AttendanceStatus.NightShift
                                                or AttendanceStatus.WeekendWork)
              / monthRecords.Count * 100m
            : 0m;

        var lateRecords = monthRecords.Where(r => r.Status == AttendanceStatus.Late).ToList();
        var avgLateMinutes = lateRecords.Count > 0 ? lateRecords.Average(r => r.LateMinutes) : 0m;
        var totalOvertime  = monthRecords.Sum(r => r.OvertimeHours);

        var suspiciousThisWeek = monthRecords
            .Count(r => r.IsSuspicious && r.Date >= sevenDaysAgo);

        // Хэлтэс тус бүрийн өнөөдрийн ирц
        var employeesByDept = await _dbContext.Employees
            .Where(e => e.IsActive)
            .Select(e => new { e.Id, e.DepartmentId })
            .ToListAsync(cancellationToken);

        var presentEmployeeIds = (await _dbContext.AttendanceRecords
            .Where(a => a.Date == today)
            .Select(a => new { a.EmployeeId, a.Status })
            .ToListAsync(cancellationToken))
            .Where(a => AttendanceStatusClassifier.CountsAsAttended(a.Status))
            .Select(a => a.EmployeeId);
        var presentSet = presentEmployeeIds.ToHashSet();

        var departmentBreakdown = departments.Select(d =>
        {
            var deptEmployees = employeesByDept.Where(e => e.DepartmentId == d.Id).ToList();
            var presentCount  = deptEmployees.Count(e => presentSet.Contains(e.Id));
            return new DepartmentSnapshot
            {
                DepartmentName = d.Name,
                EmployeeCount  = deptEmployees.Count,
                PresentToday   = presentCount,
                AttendanceRate = deptEmployees.Count > 0 ? (decimal)presentCount / deptEmployees.Count * 100m : 0m
            };
        }).Where(d => d.EmployeeCount > 0).ToList();

        // Сүүлийн 7 хоногийн чиг хандлага
        var last7DaysRecords = await _dbContext.AttendanceRecords
            .Where(a => a.Date >= sevenDaysAgo && a.Date <= today)
            .Select(a => new { a.Date, a.Status })
            .ToListAsync(cancellationToken);

        var trend = Enumerable.Range(0, 7)
            .Select(offset => sevenDaysAgo.AddDays(offset))
            .Select(date => new DailyTrendItem
            {
                Date    = date.ToString("MM-dd"),
                Present = last7DaysRecords.Count(r => r.Date == date && r.Status == AttendanceStatus.Present),
                Late    = last7DaysRecords.Count(r => r.Date == date && r.Status == AttendanceStatus.Late),
                Absent  = last7DaysRecords.Count(r => r.Date == date && r.Status == AttendanceStatus.Absent)
            })
            .ToList();

        // Энэ сар хамгийн их хоцорсон 5 ажилтан
        var employeeNames = await _dbContext.Employees
            .Select(e => new { e.Id, e.FullName })
            .ToDictionaryAsync(e => e.Id, e => e.FullName, cancellationToken);

        var topLate = monthRecords
            .Where(r => r.Status == AttendanceStatus.Late)
            .GroupBy(r => r.EmployeeId)
            .Select(g => new TopLateEmployeeItem
            {
                EmployeeName     = employeeNames.GetValueOrDefault(g.Key, "Тодорхойгүй"),
                LateCount        = g.Count(),
                TotalLateMinutes = g.Sum(r => r.LateMinutes)
            })
            .OrderByDescending(e => e.LateCount)
            .Take(5)
            .ToList();

        return new AdminAiContextDto
        {
            TotalEmployees               = totalEmployees,
            PresentToday                 = presentTodayCount,
            AbsentToday                  = Math.Max(totalEmployees - presentTodayCount - onLeaveToday - lateToday, 0),
            OnLeaveToday                 = onLeaveToday,
            LateTodayCount                = lateToday,
            PendingLeaveRequests         = pendingLeaveRequests,
            DepartmentNames              = departments.Select(d => d.Name).ToList(),
            AttendanceRateThisMonth      = attendanceRateThisMonth,
            AvgLateMinutesThisMonth      = avgLateMinutes,
            TotalOvertimeHoursThisMonth  = totalOvertime,
            SuspiciousRecordsThisWeek    = suspiciousThisWeek,
            DepartmentBreakdown          = departmentBreakdown,
            Last7DaysTrend               = trend,
            TopLateEmployeesThisMonth    = topLate
        };
    }

    private async Task<EmployeeAiContextDto> BuildEmployeeContextAsync(
        Guid employeeId, CancellationToken cancellationToken)
    {
        var today        = DateOnly.FromDateTime(DateTime.UtcNow);
        var firstOfMonth = new DateOnly(today.Year, today.Month, 1);
        var sevenDaysAgo = today.AddDays(-7);
        var fourteenDaysAgo = today.AddDays(-14);

        var employee = await _dbContext.Employees
            .Include(e => e.Department)
            .FirstOrDefaultAsync(e => e.Id == employeeId, cancellationToken);

        var todayRecord = await _dbContext.AttendanceRecords
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == today, cancellationToken);

        var lastRecord = await _dbContext.AttendanceRecords
            .Where(a => a.EmployeeId == employeeId)
            .OrderByDescending(a => a.Date)
            .FirstOrDefaultAsync(cancellationToken);

        var approvedLeaves = await _dbContext.LeaveRequests
            .Where(l => l.EmployeeId == employeeId && l.Status == RequestStatus.Approved)
            .Select(l => new { l.StartDate, l.EndDate })
            .ToListAsync(cancellationToken);

        var usedLeaveDays = approvedLeaves
            .Where(l => l.StartDate.Year == today.Year)
            .Sum(l => l.EndDate.DayNumber - l.StartDate.DayNumber + 1);

        var recentLeaves = await _dbContext.LeaveRequests
            .Where(l => l.EmployeeId == employeeId)
            .OrderByDescending(l => l.StartDate)
            .Take(5)
            .ToListAsync(cancellationToken);

        var upcomingLeave = await _dbContext.LeaveRequests
            .Where(l => l.EmployeeId == employeeId && l.Status == RequestStatus.Approved && l.StartDate > today)
            .OrderBy(l => l.StartDate)
            .Select(l => (DateOnly?)l.StartDate)
            .FirstOrDefaultAsync(cancellationToken);

        var recentAttendance = await _dbContext.AttendanceRecords
            .Where(a => a.EmployeeId == employeeId && a.Date >= sevenDaysAgo)
            .OrderByDescending(a => a.Date)
            .ToListAsync(cancellationToken);

        // Энэ сарын статистик
        var monthRecords = await _dbContext.AttendanceRecords
            .Where(a => a.EmployeeId == employeeId && a.Date >= firstOfMonth && a.Date <= today)
            .Select(a => new { a.Status, a.LateMinutes, a.OvertimeHours })
            .ToListAsync(cancellationToken);

        var lateCount       = monthRecords.Count(r => r.Status == AttendanceStatus.Late);
        var absentCount     = monthRecords.Count(r => r.Status == AttendanceStatus.Absent);
        var avgLateMinutes  = lateCount > 0 ? monthRecords.Where(r => r.Status == AttendanceStatus.Late).Average(r => r.LateMinutes) : 0m;
        var overtimeThisMonth = monthRecords.Sum(r => r.OvertimeHours);

        // Хоцролтын чиг хандлага — өмнөх 7 хоног vs түрүүчийн 7 хоног
        var prevWeekLate = await _dbContext.AttendanceRecords
            .CountAsync(a => a.EmployeeId == employeeId
                           && a.Date >= fourteenDaysAgo && a.Date < sevenDaysAgo
                           && a.Status == AttendanceStatus.Late, cancellationToken);
        var thisWeekLate = await _dbContext.AttendanceRecords
            .CountAsync(a => a.EmployeeId == employeeId
                           && a.Date >= sevenDaysAgo && a.Date <= today
                           && a.Status == AttendanceStatus.Late, cancellationToken);

        var lateTrend = thisWeekLate < prevWeekLate ? "improving"
                       : thisWeekLate > prevWeekLate ? "worsening"
                       : "stable";

        var tenureMonths = employee is not null
            ? ((today.Year - employee.HireDate.Year) * 12) + (today.Month - employee.HireDate.Month)
            : 0;

        const int annualLeave = 15;

        return new EmployeeAiContextDto
        {
            EmployeeName            = employee?.FullName ?? "Тодорхойгүй",
            Department              = employee?.Department?.Name ?? "Тодорхойгүй",
            UsedLeaveDays            = usedLeaveDays,
            TotalAnnualLeave         = annualLeave,
            LeaveBalance             = Math.Max(annualLeave - usedLeaveDays, 0),
            IsCheckedInToday         = todayRecord?.CheckInTime != null,
            LastCheckIn              = todayRecord?.CheckInTime,
            LastCheckOut             = lastRecord?.CheckOutTime,
            LateCountThisMonth       = lateCount,
            TenureMonths              = Math.Max(tenureMonths, 0),
            AvgLateMinutesThisMonth  = avgLateMinutes,
            OvertimeHoursThisMonth   = overtimeThisMonth,
            LateTrend                = lateTrend,
            AbsentCountThisMonth     = absentCount,
            UpcomingApprovedLeave    = upcomingLeave,
            RecentLeaves = recentLeaves.Select(l => new LeaveHistoryItem
            {
                StartDate = l.StartDate.ToString("yyyy-MM-dd"),
                EndDate   = l.EndDate.ToString("yyyy-MM-dd"),
                Days      = (int)l.TotalDays,
                Status    = l.Status.ToString(),
                Reason    = l.Reason ?? "-"
            }).ToList(),
            RecentAttendance = recentAttendance.Select(a => new AttendanceHistoryItem
            {
                Date      = a.Date.ToString("yyyy-MM-dd"),
                Status    = a.Status.ToString(),
                CheckIn   = a.CheckInTime.ToString("HH:mm"),
                CheckOut  = a.CheckOutTime?.ToString("HH:mm")
            }).ToList()
        };
    }
}