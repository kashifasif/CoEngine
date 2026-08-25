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
