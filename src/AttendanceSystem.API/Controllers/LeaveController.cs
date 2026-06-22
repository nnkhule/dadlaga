using System;
using System.Security.Claims;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.API.Controllers
{
    [ApiController]
    [Route("api/leave")]
    [Authorize]
    public class LeaveController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public LeaveController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost("requests")]
        [Authorize(Roles = "Employee,SuperAdmin,HRManager,DepartmentHead")]
        public async Task<IActionResult> CreateLeave([FromBody] LeaveRequestDto request)
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

            var employeeId = GetEmployeeId();
            if (employeeId is null)
                return BadRequest(new { error = "Employee profile not linked to user." });

            var leaveType = ParseLeaveType(request.Type);

            LeaveRequest leaveRequest;
            if (string.Equals(request.LeaveMode, "Hourly", StringComparison.OrdinalIgnoreCase))
            {
                if (!TimeOnly.TryParse(request.StartTime, out var startTime) ||
                    !TimeOnly.TryParse(request.EndTime, out var endTime))
                    return BadRequest(new { error = "StartTime and EndTime are required for hourly leave." });

                if (endTime <= startTime)
                    return BadRequest(new { error = "EndTime must be after StartTime." });

                var hours = request.Hours ?? (decimal)(endTime.ToTimeSpan() - startTime.ToTimeSpan()).TotalHours;

                leaveRequest = LeaveRequest.CreateHourly(employeeId.Value, leaveType, startDate, startTime, endTime, hours, request.Reason);
            }
            else
            {
                leaveRequest = LeaveRequest.Create(employeeId.Value, leaveType, startDate, endDate, request.Reason);
            }

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
                leaveRequest.Hours
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
                    l.Hours
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
                    l.Hours
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
                    l.Hours
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
                    l.Hours
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
            return Ok(new LeaveStatisticsApiDto(used, pending));
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

            request.Approve(approverId);
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(new { message = "Leave request approved." });
        }

        /// <summary>
        /// Чөлөөний хүсэлтийг татгалзана.
        /// Admin/HR (employee_id claim-гүй) ч Employee (claim-той) ч ажиллана.
        /// </summary>
        [HttpPost("requests/{id:guid}/reject")]
        [Authorize(Roles = "SuperAdmin,HRManager,DepartmentHead")]
        public async Task<IActionResult> Reject(Guid id, CancellationToken cancellationToken)
        {
            var request = await _context.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
            if (request is null)
                return NotFound(new { error = "Leave request not found." });

            if (request.Status != RequestStatus.Pending)
                return BadRequest(new { error = "Only pending leave requests can be rejected." });

            var approverId = GetEmployeeId() ?? Guid.Empty;

            request.Reject(approverId);
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(new { message = "Leave request rejected." });
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
            decimal? Hours = null);

        public sealed record LeaveStatisticsApiDto(decimal Used, int PendingRequests);
    }
}