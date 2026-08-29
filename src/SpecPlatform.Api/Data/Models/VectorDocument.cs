using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace SpecPlatform.Api.Data.Models;

public class VectorDocument
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public Guid ProjectId { get; set; }

    [Required]
    public string DocType { get; set; } = string.Empty;

    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;

    [Column(TypeName = "vector")]
    public Vector? Embedding { get; set; }

    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
}
