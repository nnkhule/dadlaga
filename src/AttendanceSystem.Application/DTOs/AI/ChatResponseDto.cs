namespace AttendanceSystem.Application.DTOs.AI;

public class ChatResponseDto
{
    public string Reply { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}