using AttendanceSystem.Application.DTOs.AI;

namespace AttendanceSystem.Application.Interfaces;

public interface IAiChatService
{
    Task<ChatResponseDto> GetAdminResponseAsync(
        ChatRequestDto request, Guid adminId, CancellationToken cancellationToken = default);

    Task<ChatResponseDto> GetEmployeeResponseAsync(
        ChatRequestDto request, Guid employeeId, CancellationToken cancellationToken = default);
}