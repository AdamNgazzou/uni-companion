using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using UniCompanion.Core.Study;
using UniCompanion.Infrastructure.Persistence;

namespace UniCompanion.Infrastructure.Ai;

public sealed class CourseSearch(AppDbContext db, IQueryEmbedding embeddings)
{
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(Guid courseId, string userId, bool isAdmin, string question, CancellationToken ct, Guid[]? versions = null)
    {
        var vector = await embeddings.EmbedAsync(question, ct);
        if (vector.Length != 384 || vector.Any(x => !float.IsFinite(x))) throw new AiUnavailableException();
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("""
            WITH eligible AS MATERIALIZED (
                SELECT c.*, m."Id" AS "MaterialId", m."FileName",
                    setweight(to_tsvector('english', m."Title"), 'A') || setweight(to_tsvector('english', c."Text"), 'B') || setweight(to_tsvector('french', c."Text"), 'B') AS search_vector
                FROM "DocumentChunks" c
                JOIN "MaterialVersions" v ON v."Id" = c."MaterialVersionId"
                JOIN "Materials" m ON m."Id" = v."MaterialId"
                WHERE m."CourseId" = @course AND c."Model" = @model
                AND ((@pinned AND c."MaterialVersionId" = ANY(@versions)) OR (NOT @pinned AND m."CurrentVersionId" = c."MaterialVersionId"))
                AND (@admin OR EXISTS (
                    SELECT 1 FROM "CourseMemberships" member
                    WHERE member."CourseId" = @course AND member."UserId" = @user))
            ), lexical AS (
                SELECT "Id", row_number() OVER (ORDER BY ts_rank_cd(search_vector, websearch_to_tsquery('english', @query) || websearch_to_tsquery('french', @query)) DESC, "Id") AS rank
                FROM eligible WHERE search_vector @@ (websearch_to_tsquery('english', @query) || websearch_to_tsquery('french', @query))
                ORDER BY rank LIMIT 30
            ), semantic AS (
                SELECT "Id", row_number() OVER (ORDER BY "Embedding"::vector <=> @embedding::vector, "Id") AS rank
                FROM eligible ORDER BY rank LIMIT 30
            ), ranked AS (
                SELECT "Id", sum(score) AS score FROM (
                    SELECT "Id", 1.0 / (60 + rank) AS score FROM lexical
                    UNION ALL SELECT "Id", 1.0 / (60 + rank) AS score FROM semantic
                ) ranks GROUP BY "Id"
            )
            SELECT "Id", "MaterialId", "MaterialVersionId", "FileName", "Page", "Text", score::double precision FROM (
                SELECT DISTINCT ON (e."MaterialVersionId", e."Page")
                    e."Id", e."MaterialId", e."MaterialVersionId", e."FileName", e."Page", e."Text", r.score
                FROM ranked r JOIN eligible e ON e."Id" = r."Id"
                ORDER BY e."MaterialVersionId", e."Page", r.score DESC, e."Id"
            ) diverse ORDER BY score DESC, "Id" LIMIT 8
            """, connection);
        command.Parameters.AddWithValue("course", courseId);
        command.Parameters.AddWithValue("pinned", versions is not null);
        command.Parameters.AddWithValue("versions", NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Uuid, versions ?? []);
        command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("admin", isAdmin);
        command.Parameters.AddWithValue("model", PythonAiBridge.Model);
        var stopWords = new HashSet<string>("what which how are the and with does for from into that this les des dans quel quels quelle quelles comment pourquoi entre une avec sur pour aux faut est sont que according used".Split(' '), StringComparer.OrdinalIgnoreCase);
        var terms = Regex.Matches(question, @"[\p{L}\p{N}_]+").Select(m => m.Value)
            .Where(word => word.Length > 2 && !stopWords.Contains(word)).Distinct(StringComparer.OrdinalIgnoreCase);
        command.Parameters.AddWithValue("query", string.Join(" OR ", terms));
        command.Parameters.AddWithValue("embedding", JsonSerializer.Serialize(vector));
        await using var reader = await command.ExecuteReaderAsync(ct);
        var hits = new List<SearchHit>();
        while (await reader.ReadAsync(ct)) hits.Add(new SearchHit(reader.GetGuid(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetString(3), reader.GetInt32(4), reader.GetString(5), reader.GetDouble(6)));
        return hits;
    }
}
