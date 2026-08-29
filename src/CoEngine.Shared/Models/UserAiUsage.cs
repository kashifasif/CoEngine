namespace CoEngine.Shared.Models;

public class UserAiUsage
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty; // e.g. "PO Brainstorming", "Spec Structuring", "Self-Review", "Dev Q&A"
    public string ModelName { get; set; } = "openrouter/anthropic/claude-3.5-sonnet";
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
