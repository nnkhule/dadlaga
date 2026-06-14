namespace AttendanceSystem.Application.DTOs.AI;

/// <summary>
/// AI response to user message.
/// </summary>
public record ChatResponseDto(
    string Response,
    bool IsSuccessful = true,
    string? ErrorMessage = null,
    DateTime RespondedAt = default);
