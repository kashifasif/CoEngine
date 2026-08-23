using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;
using SpecPlatform.Api.Data;
using SpecPlatform.Shared.Models;
using Microsoft.AI.Foundry.Local;
using OpenAI.Embeddings;
using System.ClientModel;

namespace SpecPlatform.Api.Services;

public class PgVectorStoreService : IVectorStoreService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PgVectorStoreService> _logger;
    private static dynamic? _embeddingClient;
    private static bool _isInitializing = false;
    private static readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);

    public PgVectorStoreService(AppDbContext db, ILogger<PgVectorStoreService> logger)
    {
        _db = db;
        _logger = logger;
    }

    private async Task<dynamic?> GetEmbeddingClientAsync()
    {
        if (_embeddingClient != null) return _embeddingClient;

        await _initLock.WaitAsync();
        try
        {
            if (_embeddingClient != null) return _embeddingClient;
            if (_isInitializing) return null; // Prevent re-entry if something fails

            _isInitializing = true;
            _logger.LogInformation("Initializing Microsoft.AI.Foundry.Local and downloading model if needed...");
            
            var config = new Configuration { AppName = "specplatform_embedding", LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Information };
            await FoundryLocalManager.CreateAsync(config, null);
            var mgr = FoundryLocalManager.Instance;
            
            var catalog = await mgr.GetCatalogAsync();
            var model = await catalog.GetModelAsync("qwen3-embedding-0.6b") ?? throw new Exception("Model not found in Foundry Local Catalog");

            _logger.LogInformation("Downloading model (if not cached)...");
            await model.DownloadAsync();
            
            _logger.LogInformation("Loading model...");
            await model.LoadAsync();
            
            _embeddingClient = await model.GetEmbeddingClientAsync();
            
            _logger.LogInformation("Foundry Local Embedding model initialized successfully.");
            return _embeddingClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Microsoft.AI.Foundry.Local embeddings.");
            return null;
        }
        finally
        {
            _isInitializing = false;
            _initLock.Release();
        }
    }

    public async Task IndexDocumentAsync(int projectId, string docType, string title, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        try
        {
            var fullText = $"{title} {content}";
            var client = await GetEmbeddingClientAsync();
            if (client == null) 
            {
                _logger.LogWarning("Embedding client is not ready. Skipping document indexing.");
                return;
            }

            var response = await client.GenerateEmbeddingAsync(fullText);
            var embeddingList = response.Data[0].Embedding;
            float[] vector = new float[embeddingList.Count];
            for (int i = 0; i < embeddingList.Count; i++) vector[i] = (float)embeddingList[i];
            
            var id = Guid.NewGuid().ToString();

            // Native PostgreSQL pgvector insertion
            var vectorString = "[" + string.Join(",", vector.Select(v => v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture))) + "]";

            var sql = @"
                INSERT INTO ""VectorDocuments"" (""Id"", ""ProjectId"", ""DocType"", ""Title"", ""Content"", ""Embedding"", ""IndexedAt"")
                VALUES (@id, @projectId, @docType, @title, @content, @vector::vector, @indexedAt);
            ";

            await _db.Database.ExecuteSqlRawAsync(sql,
                new NpgsqlParameter("@id", id),
                new NpgsqlParameter("@projectId", projectId),
                new NpgsqlParameter("@docType", docType),
                new NpgsqlParameter("@title", title),
                new NpgsqlParameter("@content", content),
                new NpgsqlParameter("@vector", vectorString),
                new NpgsqlParameter("@indexedAt", DateTime.UtcNow));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to index document in PostgreSQL pgvector. Falling back gracefully.");
        }
    }

    public async Task<List<VectorSearchResult>> SearchSimilarityAsync(int projectId, string query, int topK = 5)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<VectorSearchResult>();
        }

        try
        {
            var client = await GetEmbeddingClientAsync();
            if (client == null) 
            {
                _logger.LogWarning("Embedding client is not ready. Returning empty search results.");
                return new List<VectorSearchResult>();
            }

            var response = await client.GenerateEmbeddingAsync(query);
            var embeddingList = response.Data[0].Embedding;
            float[] queryVector = new float[embeddingList.Count];
            for (int i = 0; i < embeddingList.Count; i++) queryVector[i] = (float)embeddingList[i];
            
            var vectorString = "[" + string.Join(",", queryVector.Select(v => v.ToString("0.######", System.Globalization.CultureInfo.InvariantCulture))) + "]";

            var sql = @"
                SELECT 
                    ""Title"", 
                    ""Content"", 
                    ""DocType"", 
                    (1 - (""Embedding"" <=> @queryVector::vector))::float8 AS ""SimilarityScore""
                FROM ""VectorDocuments""
                WHERE ""ProjectId"" = @projectId
                ORDER BY ""Embedding"" <=> @queryVector::vector
                LIMIT @topK;
            ";

            var results = new List<VectorSearchResult>();

            var connection = _db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;

            var pProj = cmd.CreateParameter();
            pProj.ParameterName = "@projectId";
            pProj.Value = projectId;
            cmd.Parameters.Add(pProj);

            var pVec = cmd.CreateParameter();
            pVec.ParameterName = "@queryVector";
            pVec.Value = vectorString;
            cmd.Parameters.Add(pVec);

            var pTop = cmd.CreateParameter();
            pTop.ParameterName = "@topK";
            pTop.Value = topK;
            cmd.Parameters.Add(pTop);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new VectorSearchResult
                {
                    Title = reader.GetString(0),
                    Content = reader.GetString(1),
                    DocType = reader.GetString(2),
                    SimilarityScore = reader.GetDouble(3)
                });
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PostgreSQL pgvector query failed. Returning empty search results.");
            return new List<VectorSearchResult>();
        }
    }

    public async Task<VectorStoreStatsDto> GetStatsAsync(int projectId)
    {
        try
        {
            var connection = _db.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                SELECT 
                    COUNT(*)::int AS Total,
                    COUNT(CASE WHEN ""DocType"" = 'spec' THEN 1 END)::int AS SpecCount,
                    COUNT(CASE WHEN ""DocType"" = 'chat' THEN 1 END)::int AS ChatCount
                FROM ""VectorDocuments""
                WHERE ""ProjectId"" = @projectId;
            ";

            var pProj = cmd.CreateParameter();
            pProj.ParameterName = "@projectId";
            pProj.Value = projectId;
            cmd.Parameters.Add(pProj);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new VectorStoreStatsDto
                {
                    TotalVectorCount = reader.GetInt32(0),
                    SpecVectorCount = reader.GetInt32(1),
                    ChatVectorCount = reader.GetInt32(2),
                    VectorDbEngine = "PostgreSQL pgvector + Foundry Local"
                };
            }
        }
        catch { }

        return new VectorStoreStatsDto
        {
            TotalVectorCount = 0,
            SpecVectorCount = 0,
            ChatVectorCount = 0,
            VectorDbEngine = "PostgreSQL pgvector"
        };
    }

    public async Task ClearProjectVectorsAsync(int projectId)
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync(
                @"DELETE FROM ""VectorDocuments"" WHERE ""ProjectId"" = @projectId;",
                new NpgsqlParameter("@projectId", projectId));
        }
        catch { }
    }
}
