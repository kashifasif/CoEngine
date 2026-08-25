namespace SpecPlatform.Shared.Models;

public class ChatSession
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project? Project { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string PersonaMode { get; set; } = "po_brainstorming"; // po_brainstorming, dev_qa_dev, dev_qa_qa
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<ChatMessageRecord> Messages { get; set; } = new();
}

public class ChatMessageRecord
{
    public int Id { get; set; }
    public int ChatSessionId { get; set; }
    public ChatSession? ChatSession { get; set; }
    public string Role { get; set; } = "user"; // user, assistant, system
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
