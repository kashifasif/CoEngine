namespace SpecPlatform.Shared.Models;

public class Notification
{
    public int Id { get; set; }
    public int SpecId { get; set; }
    public string SpecTitle { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
