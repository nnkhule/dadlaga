using System;
using System.Security.Claims;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Domain.Enums;
using AttendanceSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    }
}
