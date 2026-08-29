namespace SpecPlatform.Shared.DTOs;

public class CreateSpecDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> AcceptanceCriteria { get; set; } = new();
    public List<string> ScopeTags { get; set; } = new();
    /// <summary>Serialized SelfReviewResultDto — stored on the SpecVersion for audit.</summary>
    public string? SelfReviewJson { get; set; }
}

public class UpdateSpecDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> AcceptanceCriteria { get; set; } = new();
    public List<string> ScopeTags { get; set; } = new();
    public Guid RowVersion { get; set; }
}

public class SpecDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; }
    public int CurrentVersionNumber { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? CreatedByDisplayName { get; set; }
    public string? CreatedByUsername { get; set; }
    public string? CreatedByAvatarUrl { get; set; }
    public List<string> CurrentAcceptanceCriteria { get; set; } = new();
    public List<string> CurrentScopeTags { get; set; } = new();
    public List<SpecVersionDto> Versions { get; set; } = new();
    public Guid RowVersion { get; set; }
}

public class SpecVersionDto
{
    public Guid Id { get; set; }
    public Guid SpecId { get; set; }
    public int VersionNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public Guid? AuthorUserId { get; set; }
    public string? AuthorDisplayName { get; set; }
    public string? AuthorUsername { get; set; }
    public string? AuthorAvatarUrl { get; set; }
    public List<string> AcceptanceCriteria { get; set; } = new();
    public List<string> ScopeTags { get; set; } = new();
    /// <summary>Serialized SelfReviewResultDto for audit display.</summary>
    public string? SelfReviewJson { get; set; }
    public bool IsUndone { get; set; }
}

public class PublishResultDto
{
    public SpecDto Spec { get; set; } = default!;
    public string NotificationSummary { get; set; } = string.Empty;
}
