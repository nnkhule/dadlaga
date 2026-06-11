using AttendanceSystem.Domain.Common;

namespace AttendanceSystem.Domain.Entities;

public class PasswordResetToken : BaseEntity
{
    public string UserId { get; private set; } = string.Empty;
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public bool IsUsed { get; private set; }

    public static PasswordResetToken Create(string userId, string token, DateTime expiresAt)
        => new() { UserId = userId, Token = token, ExpiresAt = expiresAt };

    public void MarkAsUsed()
    {
        IsUsed = true;
        SetUpdated();
    }

    public bool IsValid => !IsUsed && ExpiresAt > DateTime.UtcNow;
}
