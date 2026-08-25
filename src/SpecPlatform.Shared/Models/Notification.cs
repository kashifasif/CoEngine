namespace SpecPlatform.Shared.Models;

public class Notification
{
    public int Id { get; set; }
    public int SpecId { get; set; }
    public string SpecTitle { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public int? AuthorUserId { get; set; }
    public string? AuthorDisplayName { get; set; }
    public string? AuthorUsername { get; set; }
    public string? AuthorAvatarUrl { get; set; }
    public string ActionType { get; set; } = "published";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
