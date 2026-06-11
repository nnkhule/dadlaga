using System.Security.Cryptography;
using System.Text;
using AttendanceSystem.Application.DTOs.Auth;
using AttendanceSystem.Domain.Entities;
using AttendanceSystem.Infrastructure.Identity;
using AttendanceSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AttendanceSystem.Infrastructure.Services;

public class PasswordService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private const int PasswordResetTokenExpiryMinutes = 24 * 60;

    public PasswordService(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext dbContext)
    {
        _userManager = userManager;
        _dbContext = dbContext;
    }

    public async Task<(bool Success, string Message)> ChangePasswordAsync(
        string userId, string currentPassword, string newPassword, string confirmPassword)
    {
        if (newPassword != confirmPassword)
            return (false, "New passwords do not match.");

        if (newPassword.Length < 8)
            return (false, "New password must be at least 8 characters.");

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
            return (false, "User not found.");

        var result = await _userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
            return (false, string.Join(", ", result.Errors.Select(e => e.Description)));

        return (true, "Password changed successfully.");
    }

    public async Task<(bool Success, string Message)> ForgotPasswordAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return (true, "If the email exists, a password reset link has been sent.");

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetToken = PasswordResetToken.Create(user.Id, token, DateTime.UtcNow.AddMinutes(PasswordResetTokenExpiryMinutes));
        await _dbContext.PasswordResetTokens.AddAsync(resetToken);
        await _dbContext.SaveChangesAsync();

        return (true, "If the email exists, a password reset link has been sent.");
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(
        string email, string token, string newPassword, string confirmPassword)
    {
        if (newPassword != confirmPassword)
            return (false, "New passwords do not match.");

        if (newPassword.Length < 8)
            return (false, "Password must be at least 8 characters.");

        var user = await _userManager.FindByEmailAsync(email);
        if (user is null)
            return (false, "Invalid email or token.");

        var resetToken = await _dbContext.PasswordResetTokens
            .FirstOrDefaultAsync(t =>
                t.UserId == user.Id &&
                t.Token == token &&
                !t.IsUsed &&
                t.ExpiresAt > DateTime.UtcNow);

        if (resetToken is null)
            return (false, "Invalid or expired password reset token.");

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
            return (false, string.Join(", ", result.Errors.Select(e => e.Description)));

        resetToken.MarkAsUsed();
        await _dbContext.SaveChangesAsync();

        return (true, "Password reset successfully. Please login with your new password.");
    }

    public async Task<string?> GetResetTokenAsync(string userId)
    {
        var token = await _dbContext.PasswordResetTokens
            .Where(t => t.UserId == userId && !t.IsUsed && t.IsValid)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => t.Token)
            .FirstOrDefaultAsync();
        return token;
    }
}
