using CoEngine.Shared.Models;

namespace CoEngine.Api.Services;

public interface IVectorStoreService
{
    Task IndexDocumentAsync(Guid projectId, string docType, string title, string content);
    Task<List<VectorSearchResult>> SearchSimilarityAsync(Guid projectId, string query, int topK = 5);
    Task<VectorStoreStatsDto> GetStatsAsync(Guid projectId);
    Task ClearProjectVectorsAsync(Guid projectId);
    Task DeleteDocumentAsync(Guid projectId, string docType, string title);
}
