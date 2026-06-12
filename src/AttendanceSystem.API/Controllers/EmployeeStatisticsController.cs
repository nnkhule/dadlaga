using System;
using System.Security.Claims;
using System.Threading.Tasks;
using AttendanceSystem.API.Services;
using AttendanceSystem.API.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.API.Controllers
{
    /// <summary>
    /// Controller for employee-specific statistics endpoints.
    /// </summary>
    [ApiController]
    [Route("api/v1/statistics/employee")]
    [Authorize]
    public class EmployeeStatisticsController : ControllerBase
    {
        private readonly IEmployeeStatisticsService _service;

        /// <summary>
        /// Creates a new instance of <see cref="EmployeeStatisticsController"/>.
        /// </summary>
        public EmployeeStatisticsController(IEmployeeStatisticsService service)
        {
            _service = service;
        }

        /// <summary>
        /// Returns the statistics for the current employee.
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<EmployeeStatisticsDto>> GetStatistics()
        {
            var employeeId = GetEmployeeId();
            if (employeeId == null)
            {
                return Unauthorized(new { message = "Employee profile is not linked to this user." });
            }

            try
            {
                var statistics = await _service.GetEmployeeStatisticsAsync(employeeId.Value);
                return Ok(statistics);
            }
            catch (KeyNotFoundException knf)
            {
                return NotFound(new { message = knf.Message });
            }
            catch (Exception ex)
            {
                // For unexpected errors return 500 with minimal info
                return StatusCode(500, new { message = "An unexpected error occurred while retrieving statistics." });
            }
        }

        private Guid? GetEmployeeId()
        {
            var claim = User.FindFirstValue("employee_id");
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }
}
