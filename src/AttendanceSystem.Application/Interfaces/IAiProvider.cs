namespace AttendanceSystem.Application.Interfaces;

public interface IAiProvider
{
    Task<string> GenerateReplyAsync(
        string systemPrompt,
        List<(string Role, string Content)> messages,
        CancellationToken cancellationToken = default);
}