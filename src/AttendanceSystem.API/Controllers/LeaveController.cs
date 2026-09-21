using System;
using System.Security.Claims;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.API.Controllers
{
    [ApiController]
    [Route("api/leave")]
    [Authorize]
    public class LeaveController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public LeaveController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpPost("requests")]
        [Authorize(Roles = "Employee,SuperAdmin,HRManager,DepartmentHead")]
        public async Task<IActionResult> CreateLeave([FromForm] LeaveRequestDto request, IFormFile? document, CancellationToken cancellationToken)
        {
            if (request == null)
                return BadRequest(new { error = "Invalid payload" });

            if (string.IsNullOrWhiteSpace(request.StartDate) || string.IsNullOrWhiteSpace(request.EndDate))
                return BadRequest(new { error = "StartDate and EndDate are required." });

            if (!DateOnly.TryParse(request.StartDate, out var startDate))
                return BadRequest(new { error = "StartDate is not valid." });

            if (!DateOnly.TryParse(request.EndDate, out var endDate))
                return BadRequest(new { error = "EndDate is not valid." });

            if (endDate < startDate)
                return BadRequest(new { error = "EndDate cannot be before StartDate." });

            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length is < 10 or > 500)
                return BadRequest(new { error = "Reason must be between 10 and 500 characters." });

            var employeeId = GetEmployeeId();
            if (employeeId is null)
                return BadRequest(new { error = "Employee profile not linked to user." });

            var leaveType = ParseLeaveType(request.Type);
            var policy = await _context.LeavePolicies
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.LeaveType == leaveType && p.IsActive, cancellationToken);
            if (policy is null)
                return BadRequest(new { error = "Leave policy is not configured for this leave type." });

            if (policy.AdminOnly && User.IsInRole("Employee"))
                return Forbid();

            if (policy.RequiresReason && string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest(new { error = "A reason is required for this leave type." });

            if (policy.RequiresDocument && document == null)
                return BadRequest(new { error = "A supporting document is required for this leave type." });

            string? documentName = null;
            if (document != null)
            {
                try
                {
                    documentName = await SaveDocumentAsync(document, cancellationToken);
                }
                catch (InvalidOperationException ex)
                {
                    return BadRequest(new { error = ex.Message });
                }
            }

            var today = DateOnly.FromDateTime(DateTime.Today);
            if (startDate < today && User.IsInRole("Employee") && leaveType != LeaveType.Sick)
                return BadRequest(new { error = "New leave requests cannot start in the past." });

            if (policy.AdvanceNoticeDays > 0 && startDate.DayNumber - today.DayNumber < policy.AdvanceNoticeDays)
                return BadRequest(new { error = $"This leave must be requested at least {policy.AdvanceNoticeDays} days in advance." });

            var hasOverlap = await _context.LeaveRequests.AnyAsync(l =>
                l.EmployeeId == employeeId.Value &&
                (l.Status == RequestStatus.Pending || l.Status == RequestStatus.Approved) &&
                l.StartDate <= endDate && l.EndDate >= startDate);
            if (hasOverlap)
                return Conflict(new { error = "An active leave request already exists for this period." });

            LeaveRequest leaveRequest;
            if (string.Equals(request.LeaveMode, "Hourly", StringComparison.OrdinalIgnoreCase))
            {
                if (!TimeOnly.TryParse(request.StartTime, out var startTime) ||
                    !TimeOnly.TryParse(request.EndTime, out var endTime))
                    return BadRequest(new { error = "StartTime and EndTime are required for hourly leave." });

                if (endTime <= startTime)
                    return BadRequest(new { error = "EndTime must be after StartTime." });

                var hours = (decimal)(endTime.ToTimeSpan() - startTime.ToTimeSpan()).TotalHours;
                if (hours <= 0 || hours > 8)
                    return BadRequest(new { error = "Hourly leave must be between 1 and 8 hours." });

                leaveRequest = LeaveRequest.CreateHourly(employeeId.Value, leaveType, startDate, startTime, endTime, hours, request.Reason, documentName);
            }
            else
            {
                leaveRequest = LeaveRequest.Create(employeeId.Value, leaveType, startDate, endDate, request.Reason, documentName: documentName);
            }

            if (policy.DeductsBalance)
            {
                var year = startDate.Year;
                var requestedDays = BusinessDays(startDate, endDate);
                var balance = await _context.LeaveBalances.FirstOrDefaultAsync(b =>
                    b.EmployeeId == employeeId.Value && b.LeaveType == leaveType && b.Year == year, cancellationToken);
                if (balance is null || balance.RemainingDays < requestedDays)
                    return BadRequest(new { error = "Insufficient leave balance for this request." });
            }

            if (policy.AutoApprove)
                leaveRequest.Approve(Guid.Empty);

            await _context.LeaveRequests.AddAsync(leaveRequest);
            await _context.SaveChangesAsync();

            return Created(string.Empty, new
            {
                message = "Leave request received",
                receivedAt = DateTime.Now,
                leaveRequest.Id,
                leaveRequest.EmployeeId,
                leaveRequest.LeaveType,
                leaveRequest.StartDate,
                leaveRequest.EndDate,
                leaveRequest.Reason,
                leaveRequest.Status,
                leaveRequest.LeaveMode,
                leaveRequest.StartTime,
                leaveRequest.EndTime,
                leaveRequest.Hours,
                leaveRequest.DecisionReason,
                leaveRequest.DocumentName
            });
        }

        [HttpGet("requests")]
        [Authorize(Roles = "SuperAdmin,HRManager,DepartmentHead")]
        public async Task<IActionResult> List(
            [FromQuery] string? status,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            pageNumber = Math.Max(1, pageNumber);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = _context.LeaveRequests.AsNoTracking().Include(l => l.Employee).AsQueryable();
            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RequestStatus>(status, true, out var parsedStatus))
                query = query.Where(l => l.Status == parsedStatus);

            var total = await query.CountAsync(cancellationToken);
            var pageEntities = await query
                .OrderByDescending(l => l.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new {
                    l.Id,
                    l.EmployeeId,
                    EmployeeName = l.Employee != null ? l.Employee.FullName : null,
                    l.StartDate,
                    l.EndDate,
                    LeaveType = l.LeaveType,
                    l.Reason,
                    Status = l.Status,
                    l.LeaveMode,
                    l.StartTime,
                    l.EndTime,
                    l.Hours,
                    l.DecisionReason,
                    l.DocumentName,
                    l.CreatedAt
                })
                .ToListAsync(cancellationToken);

            var items = pageEntities.Select(l => new LeaveRequestApiDto(
                    l.Id,
                    l.EmployeeId,
                    l.EmployeeName,
                    l.StartDate,
                    l.EndDate,
                    l.LeaveType.ToString(),
                    l.Reason,
                    l.Status.ToString(),
                    l.Status.ToString(),
                    (decimal)((l.EndDate.ToDateTime(TimeOnly.MinValue) - l.StartDate.ToDateTime(TimeOnly.MinValue)).TotalDays + 1),
                    l.LeaveMode,
                    l.StartTime,
                    l.EndTime,
                    l.Hours,
                    l.DecisionReason,
                    l.DocumentName,
                    l.CreatedAt
                ))
                .ToList();

            return Ok(new PagedResponseDto<LeaveRequestApiDto>(items, pageNumber, pageSize, total));
        }

        [HttpGet("history")]
        [Authorize(Roles = "Employee,SuperAdmin,HRManager,DepartmentHead")]
        public async Task<IActionResult> History(CancellationToken cancellationToken)
        {
            var employeeId = GetEmployeeId();
            if (employeeId is null)
                return BadRequest(new { error = "Employee profile not linked to user." });

            var itemsQuery = await _context.LeaveRequests
                .AsNoTracking()
                .Where(l => l.EmployeeId == employeeId.Value)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new {
                    l.Id,
                    l.EmployeeId,
                    l.StartDate,
                    l.EndDate,
                    LeaveType = l.LeaveType,
                    l.Reason,
                    Status = l.Status,
                    l.CreatedAt,
                    l.LeaveMode,
                    l.StartTime,
                    l.EndTime,
                    l.Hours,
                    l.DecisionReason,
                    l.DocumentName
                })
                .ToListAsync(cancellationToken);

            // Compute TotalDays in memory to avoid referencing a missing DB column
            var items = itemsQuery.Select(l => new LeaveRequestApiDto(
                    l.Id,
                    l.EmployeeId,
                    null,
                    l.StartDate,
                    l.EndDate,
                    l.LeaveType.ToString(),
                    l.Reason,
                    l.Status.ToString(),
                    l.Status.ToString(),
                    (decimal)((l.EndDate.ToDateTime(TimeOnly.MinValue) - l.StartDate.ToDateTime(TimeOnly.MinValue)).TotalDays + 1),
                    l.LeaveMode,
                    l.StartTime,
                    l.EndTime,
                    l.Hours,
                    l.DecisionReason,
                    l.DocumentName,
                    l.CreatedAt
                ))
                .ToList();

            return Ok(new PagedResponseDto<LeaveRequestApiDto>(items, 1, items.Count, items.Count));
        }

        [HttpGet("statistics")]
        [Authorize(Roles = "Employee,SuperAdmin,HRManager,DepartmentHead")]
        public async Task<IActionResult> Statistics(CancellationToken cancellationToken)
        {
            var employeeId = GetEmployeeId();
            var query = _context.LeaveRequests.AsNoTracking();
            if (employeeId.HasValue)
                query = query.Where(l => l.EmployeeId == employeeId.Value);

            var approvedLeaves = await query
                .Where(l => l.Status == RequestStatus.Approved)
                .Select(l => new { l.StartDate, l.EndDate })
                .ToListAsync(cancellationToken);
            var used = approvedLeaves.Sum(l => (decimal)((l.EndDate.ToDateTime(TimeOnly.MinValue) - l.StartDate.ToDateTime(TimeOnly.MinValue)).TotalDays + 1));
            var pending = await query.CountAsync(l => l.Status == RequestStatus.Pending, cancellationToken);
            var annualBalance = employeeId.HasValue
                ? await _context.LeaveBalances.AsNoTracking().FirstOrDefaultAsync(b =>
                    b.EmployeeId == employeeId.Value && b.LeaveType == LeaveType.Annual && b.Year == DateTime.Today.Year,
                    cancellationToken)
                : null;
            var allocated = annualBalance?.AllocatedDays ?? 0;
            var usedFromBalance = annualBalance?.UsedDays ?? used;
            return Ok(new LeaveStatisticsApiDto(allocated, usedFromBalance, Math.Max(0, allocated - usedFromBalance), pending));
        }

        [HttpGet("calendar")]
        [Authorize(Roles = "Employee,SuperAdmin,HRManager,DepartmentHead")]
        public async Task<ActionResult<LeaveCalendarResponse>> Calendar(
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            CancellationToken cancellationToken)
        {
            var employeeId = GetEmployeeId();
            if (employeeId is null)
                return BadRequest(new { error = "Employee profile not linked to user." });

            var start = from ?? DateOnly.FromDateTime(DateTime.Today);
            var end = to ?? start.AddDays(90);
            var leaves = await _context.LeaveRequests.AsNoTracking()
                .Where(l => l.EmployeeId == employeeId.Value && l.Status == RequestStatus.Approved &&
                            l.StartDate <= end && l.EndDate >= start)
                .OrderBy(l => l.StartDate)
                .Select(l => new LeaveCalendarItem(l.Id, l.StartDate, l.EndDate, l.LeaveType.ToString(), l.LeaveMode))
                .ToListAsync(cancellationToken);
            var holidays = await _context.Holidays.AsNoTracking()
                .Where(h => h.Date >= start && h.Date <= end)
                .OrderBy(h => h.Date)
                .Select(h => new HolidayCalendarItem(h.Date, h.Name))
                .ToListAsync(cancellationToken);
            return Ok(new LeaveCalendarResponse(leaves, holidays));
        }

        [HttpGet("team-calendar")]
        [Authorize(Roles = "SuperAdmin,HRManager,DepartmentHead")]
        public async Task<ActionResult<IReadOnlyList<TeamLeaveCalendarItem>>> TeamCalendar(
            [FromQuery] DateOnly? from,
            [FromQuery] DateOnly? to,
            [FromQuery] Guid? departmentId,
            CancellationToken cancellationToken)
        {
            var start = from ?? DateOnly.FromDateTime(DateTime.Today);
            var end = to ?? start.AddDays(90);
            var query = _context.LeaveRequests.AsNoTracking()
                .Include(l => l.Employee)
                .Where(l => l.Status == RequestStatus.Approved && l.StartDate <= end && l.EndDate >= start);
            if (departmentId.HasValue)
                query = query.Where(l => l.Employee != null && l.Employee.DepartmentId == departmentId.Value);

            var items = await query.OrderBy(l => l.StartDate)
                .Select(l => new TeamLeaveCalendarItem(
                    l.Id,
                    l.EmployeeId,
                    l.Employee == null ? null : l.Employee.FullName,
                    l.Employee == null ? null : l.Employee.DepartmentId,
                    l.StartDate,
                    l.EndDate,
                    l.LeaveType.ToString()))
                .ToListAsync(cancellationToken);
            return Ok(items);
        }

        [HttpPut("balances/{employeeId:guid}")]
        [Authorize(Roles = "SuperAdmin,HRManager,DepartmentHead")]
        public async Task<IActionResult> AdjustBalance(
            Guid employeeId,
            [FromBody] BalanceAdjustmentRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest(new { error = "A reason is required for balance adjustments." });
            if (request.AllocatedDays < 0)
                return BadRequest(new { error = "Allocated days cannot be negative." });

            var balance = await _context.LeaveBalances.FirstOrDefaultAsync(b =>
                b.EmployeeId == employeeId && b.LeaveType == LeaveType.Annual && b.Year == DateTime.Today.Year,
                cancellationToken);
            if (balance is null)
            {
                balance = LeaveBalance.Create(employeeId, LeaveType.Annual, DateTime.Today.Year, request.AllocatedDays);
                _context.LeaveBalances.Add(balance);
                _context.AuditLogs.Add(AuditLog.Create(
                    "LeaveBalance", balance.Id, "Create",
                    null, $"AllocatedDays={request.AllocatedDays}; Reason={request.Reason.Trim()}",
                    User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString()));
            }
            else
            {
                var oldValue = balance.AllocatedDays;
                balance.Adjust(request.AllocatedDays, request.Reason.Trim());
                _context.AuditLogs.Add(AuditLog.Create(
                    "LeaveBalance", balance.Id, "Adjust",
                    $"AllocatedDays={oldValue}", $"AllocatedDays={request.AllocatedDays}; Reason={request.Reason.Trim()}",
                    User.Identity?.Name, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent.ToString()));
            }

            await _context.SaveChangesAsync(cancellationToken);
            return Ok(new { message = "Leave balance updated.", balance.AllocatedDays, balance.UsedDays, balance.RemainingDays });
        }

        /// <summary>
        /// Чөлөөний хүсэлтийг зөвшөөрнө.
        /// Admin/HR (employee_id claim-гүй) ч Employee (claim-той) ч ажиллана.
        /// </summary>
        [HttpPost("requests/{id:guid}/approve")]
        [Authorize(Roles = "SuperAdmin,HRManager,DepartmentHead")]
        public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
        {
            var request = await _context.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
            if (request is null)
                return NotFound(new { error = "Leave request not found." });

            if (request.Status != RequestStatus.Pending)
                return BadRequest(new { error = "Only pending leave requests can be approved." });

            // employee_id claim байвал ашиглана (DepartmentHead зэрэг хувийн профайлтай админ),
            // байхгүй бол Guid.Empty (SuperAdmin/HRManager-ийн системийн зөвшөөрөл).
            var approverId = GetEmployeeId() ?? Guid.Empty;

            var policy = await _context.LeavePolicies.AsNoTracking()
                .FirstOrDefaultAsync(p => p.LeaveType == request.LeaveType && p.IsActive, cancellationToken);
            if (policy?.DeductsBalance == true)
            {
                var balance = await _context.LeaveBalances.FirstOrDefaultAsync(b =>
                    b.EmployeeId == request.EmployeeId && b.LeaveType == request.LeaveType && b.Year == request.StartDate.Year,
                    cancellationToken);
                var requestedDays = BusinessDays(request.StartDate, request.EndDate);
                if (balance is null || balance.RemainingDays < requestedDays)
                    return BadRequest(new { error = "Insufficient leave balance for this approval." });
                balance.ApplyUsage(requestedDays);
            }

            request.Approve(approverId);
            _context.Notifications.Add(Notification.Create(
                request.EmployeeId,
                "Чөлөөний хүсэлт батлагдлаа",
                $"{request.StartDate:yyyy-MM-dd} - {request.EndDate:yyyy-MM-dd} хугацааны хүсэлт батлагдлаа.",
                NotificationChannel.InApp,
                request.Id,
                "LeaveRequest"));
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(new { message = "Leave request approved." });
        }

        /// <summary>
        /// Чөлөөний хүсэлтийг татгалзана.
        /// Admin/HR (employee_id claim-гүй) ч Employee (claim-той) ч ажиллана.
        /// </summary>
        [HttpPost("requests/{id:guid}/reject")]
        [Authorize(Roles = "SuperAdmin,HRManager,DepartmentHead")]
        public async Task<IActionResult> Reject(Guid id, [FromBody] DecisionRequest? decision, CancellationToken cancellationToken)
        {
            var request = await _context.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
            if (request is null)
                return NotFound(new { error = "Leave request not found." });

            if (request.Status != RequestStatus.Pending)
                return BadRequest(new { error = "Only pending leave requests can be rejected." });

            if (string.IsNullOrWhiteSpace(decision?.Reason))
                return BadRequest(new { error = "A rejection reason is required." });

            var approverId = GetEmployeeId() ?? Guid.Empty;

            request.Reject(approverId, decision.Reason.Trim());
            _context.Notifications.Add(Notification.Create(
                request.EmployeeId,
                "Чөлөөний хүсэлт татгалзсан",
                decision.Reason.Trim(),
                NotificationChannel.InApp,
                request.Id,
                "LeaveRequest"));
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(new { message = "Leave request rejected." });
        }

        [HttpPost("requests/{id:guid}/cancel")]
        [Authorize(Roles = "Employee,SuperAdmin,HRManager,DepartmentHead")]
        public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
        {
            var employeeId = GetEmployeeId();
            if (employeeId is null)
                return BadRequest(new { error = "Employee profile not linked to user." });

            var request = await _context.LeaveRequests.FirstOrDefaultAsync(l =>
                l.Id == id && l.EmployeeId == employeeId.Value, cancellationToken);
            if (request is null)
                return NotFound(new { error = "Leave request not found." });
            if (request.Status != RequestStatus.Pending)
                return BadRequest(new { error = "Only pending leave requests can be cancelled." });

            request.Cancel();
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(new { message = "Leave request cancelled." });
        }

        private Guid? GetEmployeeId()
        {
            var claim = User.FindFirstValue("employee_id");
            return Guid.TryParse(claim, out var id) ? id : null;
        }

        private static LeaveType ParseLeaveType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type))
                return LeaveType.Annual;

            return Enum.TryParse<LeaveType>(type, true, out var parsed)
                ? parsed
                : type.ToLowerInvariant() switch
                {
                    "annual" => LeaveType.Annual,
                    "sick" => LeaveType.Sick,
                    "unpaid" => LeaveType.Unpaid,
                    "birthday" => LeaveType.Birthday,
                    "maternity" => LeaveType.Maternity,
                    _ => LeaveType.Other
                };
        }

        public class LeaveRequestDto
        {
            public string? StartDate { get; set; }
            public string? EndDate { get; set; }
            public string? Type { get; set; }
            public string? Reason { get; set; }
            public string? LeaveMode { get; set; }
            public string? StartTime { get; set; }
            public string? EndTime { get; set; }
            public decimal? Hours { get; set; }
        }

        public sealed record LeaveRequestApiDto(
            Guid Id,
            Guid EmployeeId,
            string? EmployeeName,
            DateOnly StartDate,
            DateOnly EndDate,
            string LeaveType,
            string? Reason,
            string? ApprovalStatus,
            string? Status,
            decimal TotalDays,
            string? LeaveMode = null,
            TimeOnly? StartTime = null,
            TimeOnly? EndTime = null,
            decimal? Hours = null,
            string? DecisionReason = null,
            string? DocumentName = null,
            DateTime CreatedAt = default);

        public sealed record LeaveStatisticsApiDto(decimal Allocated, decimal Used, decimal Remaining, int PendingRequests);

        public sealed record LeaveCalendarResponse(IReadOnlyList<LeaveCalendarItem> ApprovedLeaves, IReadOnlyList<HolidayCalendarItem> Holidays);
        public sealed record LeaveCalendarItem(Guid Id, DateOnly StartDate, DateOnly EndDate, string LeaveType, string? LeaveMode);
        public sealed record HolidayCalendarItem(DateOnly Date, string Name);
        public sealed record TeamLeaveCalendarItem(Guid Id, Guid EmployeeId, string? EmployeeName, Guid? DepartmentId, DateOnly StartDate, DateOnly EndDate, string LeaveType);
        public sealed record BalanceAdjustmentRequest(decimal AllocatedDays, string Reason);

        public sealed record DecisionRequest(string? Reason);

        private static int BusinessDays(DateOnly start, DateOnly end)
        {
            var days = 0;
            for (var date = start; date <= end; date = date.AddDays(1))
                if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) days++;
            return days;
        }

        private async Task<string?> SaveDocumentAsync(IFormFile? document, CancellationToken cancellationToken)
        {
            if (document == null || document.Length == 0)
                return null;

            if (document.Length > 5 * 1024 * 1024)
                throw new InvalidOperationException("Supporting document cannot exceed 5 MB.");

            var extension = Path.GetExtension(document.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
            if (!allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only PDF, JPG, and PNG documents are supported.");

            var directory = Path.Combine(_environment.ContentRootPath, "App_Data", "leave-documents");
            Directory.CreateDirectory(directory);
            var storedName = $"{Guid.NewGuid():N}{extension}";

            await using var stream = document.OpenReadStream();
            using var fileStream = new FileStream(Path.Combine(directory, storedName), FileMode.Create);
            await stream.CopyToAsync(fileStream, cancellationToken);

            return storedName;
        }
    }
}