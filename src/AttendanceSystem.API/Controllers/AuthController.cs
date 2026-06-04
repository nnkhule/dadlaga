using AttendanceSystem.Application.DTOs.Auth;
using AttendanceSystem.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AttendanceSystem.API.Controllers;

/// <summary>
/// Authentication endpoints for JWT login and refresh.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly JwtTokenService _jwtTokenService;

    public AuthController(JwtTokenService jwtTokenService) => _jwtTokenService = jwtTokenService;

    /// <summary>Authenticates user and returns JWT tokens.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        var result = await _jwtTokenService.LoginAsync(request.Email, request.Password, cancellationToken);
        if (result is null)
            return Unauthorized(new { message = "Invalid credentials." });
        return Ok(result);
    }

    /// <summary>Refreshes access token using refresh token.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _jwtTokenService.RefreshAsync(request.RefreshToken, cancellationToken);
        if (result is null)
            return Unauthorized();
        return Ok(result);
    }
}

/// <summary>Refresh token request body.</summary>
public record RefreshTokenRequest(string RefreshToken);
