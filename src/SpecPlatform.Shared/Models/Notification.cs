namespace SpecPlatform.Shared.Models;

public class Notification
{
    public Guid Id { get; set; }
    public Guid SpecId { get; set; }
    public string SpecTitle { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public Guid? AuthorUserId { get; set; }
    public User? AuthorUser { get; set; }
    public string ActionType { get; set; } = "published";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
