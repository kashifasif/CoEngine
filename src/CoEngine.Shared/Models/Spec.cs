using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SpecPlatform.Shared.Models;

public class Spec
{
    [ConcurrencyCheck]
    public Guid RowVersion { get; set; } = Guid.NewGuid();
    
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft"; // Draft or Published
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }

    public Project? Project { get; set; }
    public List<SpecVersion> Versions { get; set; } = new();
}
