using System.Text.Json.Serialization;

namespace SpecPlatform.Shared.Models;

public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    [JsonIgnore]
    public List<Spec> Specs { get; set; } = new();
}
