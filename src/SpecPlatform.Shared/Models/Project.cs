using System.Text.Json.Serialization;

namespace SpecPlatform.Shared.Models;

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public List<Spec> Specs { get; set; } = new();
}
