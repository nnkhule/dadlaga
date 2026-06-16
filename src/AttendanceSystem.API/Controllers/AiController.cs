using AttendanceSystem.Application.DTOs.AI;
using AttendanceSystem.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace AttendanceSystem.API.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly IAiChatService _aiChatService;

    public AiController(IAiChatService aiChatService)
    {
        _aiChatService = aiChatService;
    }

    [HttpPost("admin/chat")]
    [Authorize(Roles = "SuperAdmin,HRManager,DepartmentHead")]  // ← "HR" → "HRManager", "Admin" → "DepartmentHead" нэмсэн
    public async Task<ActionResult<ChatResponseDto>> AdminChat(
        [FromBody] ChatRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest("Message хоосон байж болохгүй.");

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var response = await _aiChatService.GetAdminResponseAsync(request, userId, cancellationToken);
        return Ok(response);
    }

    [HttpPost("employee/chat")]
    [Authorize(Roles = "Employee,SuperAdmin,HRManager,DepartmentHead")]  // ← "Admin","HR" → зөв role-ууд    
    public async Task<ActionResult<ChatResponseDto>> EmployeeChat(
        [FromBody] ChatRequestDto request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest("Message хоосон байж болохгүй.");

        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();

        var response = await _aiChatService.GetEmployeeResponseAsync(request, userId, cancellationToken);
        return Ok(response);
    }
}