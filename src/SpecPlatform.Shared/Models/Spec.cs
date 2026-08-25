using System.Text.Json.Serialization;

namespace SpecPlatform.Shared.Models;

public class Spec
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft"; // Draft or Published
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public Project? Project { get; set; }
    public List<SpecVersion> Versions { get; set; } = new();
}
