using AttendanceSystem.Application.DTOs.Auth;
using AttendanceSystem.Infrastructure.Identity;
using AttendanceSystem.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    private readonly PasswordService _passwordService;
    private readonly UserManager<ApplicationUser> _userManager;

    public AuthController(
        JwtTokenService jwtTokenService,
        PasswordService passwordService,
        UserManager<ApplicationUser> userManager)
    {
        _jwtTokenService = jwtTokenService;
        _passwordService = passwordService;
        _userManager = userManager;
    }

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
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest? request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken))
            return Unauthorized();

        var result = await _jwtTokenService.RefreshAsync(request.RefreshToken, cancellationToken);
        if (result is null)
            return Unauthorized();
        return Ok(result);
    }

    /// <summary>Sends password reset link to user email.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto request)
    {
        var (success, message) = await _passwordService.ForgotPasswordAsync(request.Email);
        return Ok(new ForgotPasswordResponseDto(success, message));
    }

    /// <summary>Resets user password with reset token.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto request)
    {
        var (success, message) = await _passwordService.ResetPasswordAsync(
            request.Email, request.Token, request.NewPassword, request.ConfirmPassword);
        return success ? Ok(new ResetPasswordResponseDto(success, message))
            : BadRequest(new ResetPasswordResponseDto(success, message));
    }

    /// <summary>Creates login account for an employee.</summary>
    [HttpPost("setup-employee-account")]
    [Authorize(Roles = "SuperAdmin,HRManager")]
    public async Task<IActionResult> SetupEmployeeAccount([FromBody] SetupEmployeeAccountDto request)
    {
        if (string.IsNullOrWhiteSpace(request.EmployeeId) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "EmployeeId, email, and password are required." });

        if (!Guid.TryParse(request.EmployeeId, out var employeeId))
            return BadRequest(new { message = "Invalid employee ID." });

        if (request.Password.Length < 8)
            return BadRequest(new { message = "Password must be at least 8 characters." });

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is not null)
            return BadRequest(new { message = "Email already in use." });

        var newUser = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FullName = request.FullName ?? request.Email,
            EmployeeId = employeeId,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(newUser, request.Password);
        if (!createResult.Succeeded)
            return BadRequest(new { message = string.Join(", ", createResult.Errors.Select(e => e.Description)) });

        await _userManager.AddToRoleAsync(newUser, "Employee");
        return Ok(new { message = "Employee account created successfully." });
    }
}

/// <summary>Refresh token request body.</summary>
public record RefreshTokenRequest(string RefreshToken);
