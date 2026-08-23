using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;
using SpecPlatform.Api.Data;
using SpecPlatform.Shared.Models;

namespace SpecPlatform.Api.Services;

public class PgVectorStoreService : IVectorStoreService
{
    private readonly AppDbContext _db;
    private readonly ILogger<PgVectorStoreService> _logger;
    private static readonly Regex TokenRegex = new Regex(@"\w+", RegexOptions.Compiled);

    public PgVectorStoreService(AppDbContext db, ILogger<PgVectorStoreService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task IndexDocumentAsync(int projectId, string docType, string title, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return;

        try
        {
            var fullText = $"{title} {content}";
            var vector = GenerateEmbedding(fullText);
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
            var queryVector = GenerateEmbedding(query);
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
                    VectorDbEngine = "PostgreSQL pgvector (HNSW / Cosine Distance)"
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

    private static float[] GenerateEmbedding(string text)
    {
        var tokens = TokenRegex.Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(t => t.Length > 2)
            .ToList();

        if (!tokens.Any()) return new float[128];

        var vector = new float[128];
        foreach (var token in tokens)
        {
            int hash = Math.Abs(token.GetHashCode()) % 128;
            vector[hash] += 1.0f;
        }

        // Normalize vector to unit length
        float magnitude = MathF.Sqrt(vector.Sum(v => v * v));
        if (magnitude > 0)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] /= magnitude;
            }
        }

        return vector;
    }
}
