using AttendanceSystem.Application.DTOs.AI;

namespace AttendanceSystem.Application.Interfaces;

/// <summary>
/// Abstraction layer for AI providers (OpenAI, Ollama, NVIDIA NIM, etc).
/// </summary>
public interface IAiProvider
{
    /// <summary>
    /// Generates a response for an employee based on their data.
    /// </summary>
    Task<string> GenerateEmployeeResponseAsync(
        string message,
        EmployeeAiContextDto context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a response for an admin based on organizational data.
    /// </summary>
    Task<string> GenerateAdminResponseAsync(
        string message,
        AdminAiContextDto context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the provider is available/configured.
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
