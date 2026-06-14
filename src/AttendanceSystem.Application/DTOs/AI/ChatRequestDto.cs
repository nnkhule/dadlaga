namespace AttendanceSystem.Application.DTOs.AI;

/// <summary>
/// User message request for AI chat.
/// </summary>
public record ChatRequestDto(
    string Message,
    string? Context = null);
