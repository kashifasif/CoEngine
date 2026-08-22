namespace SpecPlatform.Shared.Models;

public class VectorDocumentRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int ProjectId { get; set; }
    public string DocType { get; set; } = "spec"; // "spec" or "chat"
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public double[] Embedding { get; set; } = Array.Empty<double>();
    public DateTime IndexedAt { get; set; } = DateTime.UtcNow;
}

public class VectorSearchResult
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string DocType { get; set; } = "spec";
    public double SimilarityScore { get; set; }
}

public class VectorStoreStatsDto
{
    public int TotalVectorCount { get; set; }
    public int SpecVectorCount { get; set; }
    public int ChatVectorCount { get; set; }
    public string VectorDbEngine { get; set; } = "Local Vector DB (Cosine Similarity)";
}
