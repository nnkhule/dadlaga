using System.Security.Claims;
using AttendanceSystem.Application.DTOs.AI;
using AttendanceSystem.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.API.Controllers;

/// <summary>
/// AI Chat controller for employee and admin queries.
/// Uses JWT claims to identify user and scope data appropriately.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IAiChatService _aiChatService;
    private readonly ILogger<AiController> _logger;

    public AiController(
        IAiChatService aiChatService,
        ILogger<AiController> logger)
    {
        _aiChatService = aiChatService;
        _logger = logger;
    }

    /// <summary>
    /// Chat endpoint for employees to query their attendance data.
    /// </summary>
    [HttpPost("chat")]
    [ProducesResponseType(typeof(ChatResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ChatResponseDto>> Chat([FromBody] ChatRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new ChatResponseDto(
                Response: "Message cannot be empty.",
                IsSuccessful: false,
                ErrorMessage: "Invalid request"));

        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim is null)
                return Unauthorized(new ChatResponseDto(
                    Response: "User identification failed.",
                    IsSuccessful: false,
                    ErrorMessage: "No user ID in claims"));

            if (!Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized(new ChatResponseDto(
                    Response: "Invalid user ID format.",
                    IsSuccessful: false,
                    ErrorMessage: "Invalid user ID"));

            var employeeIdClaim = User.FindFirst("employee_id");
            var isAdmin = User.IsInRole("SuperAdmin") || User.IsInRole("Admin") || User.IsInRole("HRManager");

            string response;

            if (isAdmin)
            {
                _logger.LogInformation("Admin {UserId} querying AI chat", userId);
                response = await _aiChatService.ProcessAdminChatAsync(request.Message, cancellationToken);
            }
            else if (employeeIdClaim is not null && Guid.TryParse(employeeIdClaim.Value, out var employeeId))
            {
                _logger.LogInformation("Employee {EmployeeId} querying AI chat", employeeId);
                response = await _aiChatService.ProcessEmployeeChatAsync(employeeId, request.Message, cancellationToken);
            }
            else
            {
                return Unauthorized(new ChatResponseDto(
                    Response: "Employee information not found in your account.",
                    IsSuccessful: false,
                    ErrorMessage: "No employee ID in claims"));
            }

            return Ok(new ChatResponseDto(
                Response: response,
                IsSuccessful: true,
                RespondedAt: DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in AI chat endpoint");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new ChatResponseDto(
                    Response: "An internal error occurred.",
                    IsSuccessful: false,
                    ErrorMessage: ex.Message));
        }
    }

    /// <summary>
    /// Health check for AI service availability.
    /// </summary>
    [HttpGet("health")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> Health(CancellationToken cancellationToken)
    {
        return Ok(new { status = "AI service is available" });
    }
}
