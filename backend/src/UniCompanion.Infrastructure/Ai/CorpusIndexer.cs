using Microsoft.EntityFrameworkCore;
using UniCompanion.Core.Study;
using UniCompanion.Infrastructure.Persistence;
using UniCompanion.Infrastructure.Storage;

namespace UniCompanion.Infrastructure.Ai;

public sealed class CorpusIndexer(AppDbContext db, PrivateFileStore files, PythonAiBridge bridge)
{
    public async Task<(int Documents, int Chunks)> IndexAsync(CancellationToken ct = default)
    {
        var materials = await db.Materials.AsNoTracking().Where(m => m.ContentType == "application/pdf" && m.CurrentVersionId != null).ToListAsync(ct);
        var documentCount = 0;
        var chunkCount = 0;
        foreach (var material in materials)
        {
            var version = await db.MaterialVersions.AsNoTracking().SingleAsync(v => v.Id == material.CurrentVersionId, ct);
            var existing = await db.DocumentChunks.CountAsync(c => c.MaterialVersionId == version.Id && c.Model == PythonAiBridge.Model, ct);
            if (existing > 0) { documentCount++; chunkCount += existing; continue; }
            var document = await bridge.IndexAsync(files.PathFor(version.Sha256), ct);
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            // A second indexer may finish the same version while extraction is in flight.
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(427815917)", ct);
            await db.DocumentChunks.Where(c => c.MaterialVersionId == version.Id && c.Model == PythonAiBridge.Model).ExecuteDeleteAsync(ct);
            db.DocumentChunks.AddRange(document.Chunks.Select((chunk, index) => new DocumentChunk
            {
                MaterialVersionId = version.Id,
                Ordinal = index,
                Page = chunk.Page,
                Text = chunk.Text,
                Embedding = chunk.Vector,
                Model = document.Model
            }));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            db.ChangeTracker.Clear();
            documentCount++;
            chunkCount += document.Chunks.Length;
            Console.WriteLine($"Indexed document {documentCount}/{materials.Count}: {document.PagesWithText}/{document.Pages} pages, {document.Chunks.Length} chunks.");
        }
        return (documentCount, chunkCount);
    }
}
