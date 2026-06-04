using AttendanceSystem.Domain.Common;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// JWT refresh token with rotation support.
/// </summary>
public class RefreshToken : BaseEntity
{
    public string UserId { get; private set; } = string.Empty;
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public bool IsRevoked { get; private set; }
    public string? ReplacedByToken { get; private set; }

    public static RefreshToken Create(string userId, string token, DateTime expiresAt)
        => new() { UserId = userId, Token = token, ExpiresAt = expiresAt };

    public void Revoke(string? replacedBy = null)
    {
        IsRevoked = true;
        ReplacedByToken = replacedBy;
        SetUpdated();
    }

    public bool IsActive => !IsRevoked && ExpiresAt > DateTime.UtcNow;
}
