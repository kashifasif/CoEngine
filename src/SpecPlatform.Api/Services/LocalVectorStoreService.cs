using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using SpecPlatform.Shared.Models;

namespace SpecPlatform.Api.Services;

public interface IVectorStoreService
{
    Task IndexDocumentAsync(int projectId, string docType, string title, string content);
    Task<List<VectorSearchResult>> SearchSimilarityAsync(int projectId, string query, int topK = 5);
    Task<VectorStoreStatsDto> GetStatsAsync(int projectId);
    Task ClearProjectVectorsAsync(int projectId);
    Task DeleteDocumentAsync(int projectId, string docType, string title);
}

public class LocalVectorStoreService : IVectorStoreService
{
    private static readonly ConcurrentBag<VectorDocumentRecord> VectorStore = new();
    private static readonly Regex TokenRegex = new Regex(@"\w+", RegexOptions.Compiled);

    public Task IndexDocumentAsync(int projectId, string docType, string title, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return Task.CompletedTask;

        var fullText = $"{title} {content}";
        var vector = GenerateEmbedding(fullText);

        var record = new VectorDocumentRecord
        {
            Id = Guid.NewGuid().ToString(),
            ProjectId = projectId,
            DocType = docType,
            Title = title,
            Content = content,
            Embedding = vector,
            IndexedAt = DateTime.UtcNow
        };

        VectorStore.Add(record);
        return Task.CompletedTask;
    }

    public Task<List<VectorSearchResult>> SearchSimilarityAsync(int projectId, string query, int topK = 5)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(new List<VectorSearchResult>());
        }

        var queryVector = GenerateEmbedding(query);
        var projectRecords = VectorStore.Where(v => v.ProjectId == projectId).ToList();

        if (!projectRecords.Any())
        {
            return Task.FromResult(new List<VectorSearchResult>());
        }

        var results = new List<VectorSearchResult>();
        foreach (var rec in projectRecords)
        {
            var similarity = ComputeCosineSimilarity(queryVector, rec.Embedding);
            results.Add(new VectorSearchResult
            {
                Title = rec.Title,
                Content = rec.Content,
                DocType = rec.DocType,
                SimilarityScore = similarity
            });
        }

        var topResults = results
            .OrderByDescending(r => r.SimilarityScore)
            .Take(topK)
            .ToList();

        return Task.FromResult(topResults);
    }

    public Task<VectorStoreStatsDto> GetStatsAsync(int projectId)
    {
        var projectRecords = VectorStore.Where(v => v.ProjectId == projectId).ToList();
        var stats = new VectorStoreStatsDto
        {
            TotalVectorCount = projectRecords.Count,
            SpecVectorCount = projectRecords.Count(r => r.DocType == "spec"),
            ChatVectorCount = projectRecords.Count(r => r.DocType == "chat"),
            VectorDbEngine = "Local Vector DB (Cosine Similarity Embeddings)"
        };
        return Task.FromResult(stats);
    }

    public Task ClearProjectVectorsAsync(int projectId)
    {
        var itemsToKeep = VectorStore.Where(v => v.ProjectId != projectId).ToList();
        VectorStore.Clear();
        foreach (var item in itemsToKeep)
        {
            VectorStore.Add(item);
        }
        return Task.CompletedTask;
    }

    public Task DeleteDocumentAsync(int projectId, string docType, string title)
    {
        var itemToRemove = VectorStore.FirstOrDefault(v => v.ProjectId == projectId && v.DocType == docType && v.Title == title);
        if (itemToRemove != null)
        {
            var itemsToKeep = VectorStore.Where(v => v.Id != itemToRemove.Id).ToList();
            VectorStore.Clear();
            foreach (var item in itemsToKeep)
            {
                VectorStore.Add(item);
            }
        }
        return Task.CompletedTask;
    }

    private static double[] GenerateEmbedding(string text)
    {
        var tokens = TokenRegex.Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(t => t.Length > 2)
            .ToList();

        if (!tokens.Any()) return new double[128];

        var vector = new double[128];
        foreach (var token in tokens)
        {
            int hash = Math.Abs(token.GetHashCode()) % 128;
            vector[hash] += 1.0;
        }

        // Normalize vector to unit length
        double magnitude = Math.Sqrt(vector.Sum(v => v * v));
        if (magnitude > 0)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] /= magnitude;
            }
        }

        return vector;
    }

    private static double ComputeCosineSimilarity(double[] vectorA, double[] vectorB)
    {
        if (vectorA.Length != vectorB.Length || vectorA.Length == 0) return 0.0;

        double dotProduct = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            normA += vectorA[i] * vectorA[i];
            normB += vectorB[i] * vectorB[i];
        }

        if (normA == 0.0 || normB == 0.0) return 0.0;

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
