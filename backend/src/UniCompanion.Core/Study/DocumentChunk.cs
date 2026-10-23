namespace UniCompanion.Core.Study;

public sealed class DocumentChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaterialVersionId { get; set; }
    public int Ordinal { get; set; }
    public int Page { get; set; }
    public string Text { get; set; } = "";
    public string Model { get; set; } = "";
    public float[] Embedding { get; set; } = [];
}

public sealed record SearchHit(Guid ChunkId, Guid MaterialId, Guid VersionId, string FileName, int Page, string Text, double Score);

public interface IQueryEmbedding
{
    Task<float[]> EmbedAsync(string question, CancellationToken cancellationToken);
}
