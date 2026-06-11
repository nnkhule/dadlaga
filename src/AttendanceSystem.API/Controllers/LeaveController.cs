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
            var leaveRequest = LeaveRequest.Create(employeeId.Value, leaveType, startDate, endDate, request.Reason);

            await _context.LeaveRequests.AddAsync(leaveRequest);
            await _context.SaveChangesAsync();

            return Created(string.Empty, new
            {
                message = "Leave request received",
                receivedAt = DateTime.UtcNow,
                leaveRequest.Id,
                leaveRequest.EmployeeId,
                leaveRequest.LeaveType,
                leaveRequest.StartDate,
                leaveRequest.EndDate,
                leaveRequest.Reason,
                leaveRequest.Status
            });
        }

        [HttpGet("requests")]
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
            var items = await query
                .OrderByDescending(l => l.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new LeaveRequestApiDto(
                    l.Id,
                    l.EmployeeId,
                    l.Employee == null ? null : l.Employee.FullName,
                    l.StartDate,
                    l.EndDate,
                    l.LeaveType.ToString(),
                    l.Reason,
                    l.Status.ToString(),
                    l.Status.ToString(),
                    l.TotalDays))
                .ToListAsync(cancellationToken);

            return Ok(new PagedResponseDto<LeaveRequestApiDto>(items, pageNumber, pageSize, total));
        }

        [HttpGet("history")]
        public async Task<IActionResult> History(CancellationToken cancellationToken)
        {
            var employeeId = GetEmployeeId();
            if (employeeId is null)
                return BadRequest(new { error = "Employee profile not linked to user." });

            var items = await _context.LeaveRequests
                .AsNoTracking()
                .Where(l => l.EmployeeId == employeeId.Value)
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new LeaveRequestApiDto(
                    l.Id,
                    l.EmployeeId,
                    null,
                    l.StartDate,
                    l.EndDate,
                    l.LeaveType.ToString(),
                    l.Reason,
                    l.Status.ToString(),
                    l.Status.ToString(),
                    l.TotalDays))
                .ToListAsync(cancellationToken);

            return Ok(new PagedResponseDto<LeaveRequestApiDto>(items, 1, items.Count, items.Count));
        }

        [HttpGet("statistics")]
        public async Task<IActionResult> Statistics(CancellationToken cancellationToken)
        {
            var employeeId = GetEmployeeId();
            var query = _context.LeaveRequests.AsNoTracking();
            if (employeeId.HasValue)
                query = query.Where(l => l.EmployeeId == employeeId.Value);

            var used = await query.Where(l => l.Status == RequestStatus.Approved).SumAsync(l => l.TotalDays, cancellationToken);
            var pending = await query.CountAsync(l => l.Status == RequestStatus.Pending, cancellationToken);
            return Ok(new LeaveStatisticsApiDto(used, pending));
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
            decimal TotalDays);

        public sealed record LeaveStatisticsApiDto(decimal Used, int PendingRequests);
    }
}
