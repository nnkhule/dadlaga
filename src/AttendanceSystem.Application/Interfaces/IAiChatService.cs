namespace AttendanceSystem.Application.Interfaces;

/// <summary>
/// Service for handling AI chat interactions with real database data.
/// </summary>
public interface IAiChatService
{
    /// <summary>
    /// Processes an employee chat message with their personal attendance data.
    /// </summary>
    Task<string> ProcessEmployeeChatAsync(
        Guid employeeId,
        string message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes an admin chat message with organization-wide analytics.
    /// </summary>
    Task<string> ProcessAdminChatAsync(
        string message,
        CancellationToken cancellationToken = default);
}
