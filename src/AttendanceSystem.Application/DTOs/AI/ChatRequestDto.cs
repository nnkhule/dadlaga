namespace AttendanceSystem.Application.DTOs.AI;

public class ChatRequestDto
{
    public string Message { get; set; } = string.Empty;
    public List<ChatMessageDto>? History { get; set; }
}

public class ChatMessageDto
{
    public string Role { get; set; } = string.Empty; // "user" эсвэл "assistant"
    public string Content { get; set; } = string.Empty;
}