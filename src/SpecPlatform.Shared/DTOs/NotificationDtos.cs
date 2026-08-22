namespace SpecPlatform.Shared.DTOs;

public class NotificationDto
{
    public int Id { get; set; }
    public int SpecId { get; set; }
    public string SpecTitle { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public int VersionNumber { get; set; }
    public string SummaryText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class SpecDiffDto
{
    public int SpecId { get; set; }
    public int FromVersion { get; set; }
    public int ToVersion { get; set; }
    public List<string> AddedCriteria { get; set; } = new();
    public List<string> RemovedCriteria { get; set; } = new();
    public List<string> AddedScopeTags { get; set; } = new();
    public List<string> RemovedScopeTags { get; set; } = new();
    public string AiSummary { get; set; } = string.Empty;
}
