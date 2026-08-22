namespace SpecPlatform.Shared.DTOs;

public class ChatMessageDto
{
    public string Role { get; set; } = "user"; // user, assistant, system
    public string Content { get; set; } = string.Empty;
}

public class ChatRequestDto
{
    public int ProjectId { get; set; }
    public List<ChatMessageDto> Messages { get; set; } = new();
}

public class DevQaQueryRequestDto
{
    public int ProjectId { get; set; }
    public string RoleMode { get; set; } = "qa_mode"; // qa_mode
    public List<ChatMessageDto> Messages { get; set; } = new();
}

public class ChatSessionDto
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string PersonaMode { get; set; } = "po_brainstorming";
    public DateTime UpdatedAt { get; set; }
    public List<ChatMessageDto> Messages { get; set; } = new();
}

public class AppendMessageDto
{
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
}

public class ChatResponseDto
{
    public string Reply { get; set; } = string.Empty;
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
}

public class StructuredSpecResultDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> AcceptanceCriteria { get; set; } = new();
    public List<string> ScopeTags { get; set; } = new();
}
