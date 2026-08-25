using System.Text.Json.Serialization;

namespace SpecPlatform.Shared.Models;

public class SpecVersion
{
    public int Id { get; set; }
    public int SpecId { get; set; }
    public int VersionNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public Spec? Spec { get; set; }
    public List<AcceptanceCriterion> AcceptanceCriteria { get; set; } = new();
    public List<ScopeTag> ScopeTags { get; set; } = new();

    /// <summary>
    /// JSON-serialized SelfReviewResultDto stored for audit. Null for versions
    /// published before the self-review feature was introduced.
    /// </summary>
    public string? SelfReviewJson { get; set; }

    public bool IsUndone { get; set; } = false;
}
