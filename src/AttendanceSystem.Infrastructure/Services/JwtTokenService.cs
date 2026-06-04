using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AttendanceSystem.Application.Configuration;
using AttendanceSystem.Application.DTOs.Auth;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Infrastructure.Identity;
using AttendanceSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AttendanceSystem.Infrastructure.Services;

/// <summary>
/// JWT and refresh token generation and validation.
/// </summary>
public class JwtTokenService
{
    private readonly JwtSettings _settings;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;

    public JwtTokenService(
        IOptions<JwtSettings> settings,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext)
    {
        _settings = settings.Value;
        _userManager = userManager;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Authenticates user and returns token pair.
    /// </summary>
    public async Task<TokenResponseDto?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, password))
            return null;

        return await GenerateTokensAsync(user, cancellationToken);
    }

    /// <summary>
    /// Refreshes access token using a valid refresh token.
    /// </summary>
    public async Task<TokenResponseDto?> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var stored = _dbContext.RefreshTokens.FirstOrDefault(t => t.Token == refreshToken && t.IsActive);
        if (stored is null)
            return null;

        var user = await _userManager.FindByIdAsync(stored.UserId);
        if (user is null)
            return null;

        stored.Revoke();
        return await GenerateTokensAsync(user, cancellationToken);
    }

    private async Task<TokenResponseDto> GenerateTokensAsync(ApplicationUser user, CancellationToken cancellationToken)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        if (user.EmployeeId.HasValue)
        {
            claims.Add(new Claim("employee_id", user.EmployeeId.Value.ToString()));

            var employee = await _dbContext.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == user.EmployeeId.Value, cancellationToken);

            if (employee is not null)
            {
                claims.Add(new Claim("department_id", employee.DepartmentId.ToString()));
            }
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpiryMinutes);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        var refresh = RefreshToken.Create(user.Id, GenerateRefreshTokenString(),
            DateTime.UtcNow.AddDays(_settings.RefreshTokenExpiryDays));
        await _dbContext.RefreshTokens.AddAsync(refresh, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TokenResponseDto(
            new JwtSecurityTokenHandler().WriteToken(token),
            refresh.Token,
            expires,
            user.EmployeeId);
    }

    private static string GenerateRefreshTokenString()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }
}
