using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using UniCompanion.Core.Study;

namespace UniCompanion.Infrastructure.Ai;

public sealed class AiUnavailableException : Exception;

public sealed class PythonAiBridge(IConfiguration configuration) : IQueryEmbedding, ITextGeneration, IDisposable
{
    public const string Model = "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2@fastembed-0.9.0";
    private readonly SemaphoreSlim gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<float[]> EmbedAsync(string question, CancellationToken cancellationToken)
    {
        JsonElement result;
        try { result = await InvokeAsync(new { operation = "embed", text = question }, TimeSpan.FromSeconds(90), cancellationToken); }
        catch (GenerationFailure) { throw new AiUnavailableException(); }
        if (result.TryGetProperty("error", out var queryError) && queryError.GetString() == "query_too_long")
            throw new StudyRequestException(400, "question_too_long");
        if (!result.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True) throw new AiUnavailableException();
        var response = result.Deserialize<EmbeddingResponse>(JsonOptions) ?? throw new AiUnavailableException();
        if (response.Model != Model || response.Vector is not { Length: 384 } || response.Vector.Any(v => !float.IsFinite(v)))
            throw new AiUnavailableException();
        return response.Vector;
    }

    public async Task<IndexedDocument> IndexAsync(string privatePath, CancellationToken cancellationToken)
    {
        var result = await SendAsync(new { operation = "index", path = privatePath }, TimeSpan.FromMinutes(5), cancellationToken);
        var response = result.Deserialize<IndexedDocument>(JsonOptions) ?? throw new AiUnavailableException();
        if (response.Model != Model || response.Pages < 1 || response.PagesWithText < 0 || response.PagesWithText > response.Pages || response.Chunks is null || response.Chunks.Length > 10000 || response.Chunks.Any(c =>
            c.Page < 1 || c.Page > response.Pages || string.IsNullOrWhiteSpace(c.Text) || c.Vector is not { Length: 384 } || c.Vector.Any(v => !float.IsFinite(v))))
            throw new AiUnavailableException();
        return response;
    }

    public async Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken)
    {
        try
        {
            var response = await InvokeAsync(new { operation = "generate", prompt }, TimeSpan.FromSeconds(90), cancellationToken);
            if (!response.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True)
            {
                var retryable = response.TryGetProperty("retryable", out var retry) && retry.ValueKind == JsonValueKind.True;
                int? delay = response.TryGetProperty("retryAfter", out var after) && after.ValueKind == JsonValueKind.Number && after.TryGetInt32(out var seconds) ? Math.Clamp(seconds, 0, 3600) : null;
                var code = response.TryGetProperty("error", out var error) ? error.GetString() : "provider_unavailable";
                throw new GenerationFailure(code ?? "provider_unavailable", retryable, delay);
            }
            if (!response.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(text.GetString()))
                throw new GenerationFailure("empty_provider_response", true);
            return text.GetString()!;
        }
        catch (AiUnavailableException) { throw new GenerationFailure("provider_unavailable", true); }
        catch (InvalidOperationException) { throw new GenerationFailure("invalid_provider_response", false); }
    }

    public async Task<JsonElement> SendAsync(object request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            var result = await InvokeAsync(request, timeout, cancellationToken);
            if (!result.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True) throw new AiUnavailableException();
            return result;
        }
        catch (GenerationFailure) { throw new AiUnavailableException(); }
    }

    private async Task<JsonElement> InvokeAsync(object request, TimeSpan timeout, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        using var process = new Process();
        var started = false;
        FileStream? hostLease = null;
        Task[] processTasks = [];
        try
        {
            hostLease = await AcquireHostLeaseAsync(deadline.Token);
            var executable = configuration["Ai:Python"] ?? throw new InvalidOperationException("Ai:Python must name the private Python interpreter.");
            var script = configuration["Ai:Bridge"] ?? throw new InvalidOperationException("Ai:Bridge must name the private bridge script.");
            process.StartInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            process.StartInfo.ArgumentList.Add(Path.GetFullPath(script));
            started = process.Start();
            if (!started) throw new AiUnavailableException();
            var output = ReadBoundedAsync(process.StandardOutput, 12_000_000, deadline.Token);
            var diagnostics = ReadBoundedAsync(process.StandardError, 64_000, deadline.Token);
            processTasks = [process.WaitForExitAsync(deadline.Token), output, diagnostics,
                WriteInputAsync(process.StandardInput, request, deadline.Token)];
            // WhenAll waits for every task even after a reader faults. Observe each
            // completion so an output-limit failure kills a still-running child now.
            var pending = processTasks.ToList();
            while (pending.Count > 0)
            {
                var completed = await Task.WhenAny(pending);
                await completed;
                pending.Remove(completed);
            }
            if (process.ExitCode != 0) throw new AiUnavailableException();
            using var json = JsonDocument.Parse(await output);
            var root = json.RootElement;
            return root.Clone();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GenerationFailure(hostLease is null ? "ai_busy" : "provider_timeout", true, hostLease is null ? 5 : null);
        }
        catch (JsonException) { throw new GenerationFailure("invalid_provider_response", false); }
        catch (System.ComponentModel.Win32Exception) { throw new AiUnavailableException(); }
        catch (IOException) { throw new AiUnavailableException(); }
        finally
        {
            try
            {
                await deadline.CancelAsync();
                if (started)
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await process.WaitForExitAsync(cleanup.Token);
                }
            }
            catch (InvalidOperationException) { /* Process exited between inspection and kill. */ }
            catch (OperationCanceledException) { /* Cleanup is bounded; preserve the original result. */ }
            finally
            {
                // Observe cancelled/faulted pipe tasks without replacing the primary
                // provider failure. Cancellation plus process termination closes pipes.
                try { await Task.WhenAll(processTasks).WaitAsync(TimeSpan.FromSeconds(5)); }
                catch (Exception) { /* The original failure is already being propagated. */ }
                hostLease?.Dispose(); gate.Release();
            }
        }
    }

    private static async Task WriteInputAsync(StreamWriter input, object request, CancellationToken cancellationToken)
    {
        // Supervise input alongside both readers: a child that stops reading must
        // not hide an output-limit failure behind a blocked pipe write.
        await input.WriteLineAsync(JsonSerializer.Serialize(request).AsMemory(), cancellationToken);
        input.Close();
    }

    private async Task<FileStream> AcquireHostLeaseAsync(CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(configuration["Storage:Root"] ?? ".local");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "ai-process.lock");
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { await Task.Delay(100, cancellationToken); }
        }
    }

    private static async Task<string> ReadBoundedAsync(StreamReader reader, int maximum, CancellationToken cancellationToken)
    {
        var buffer = new char[4096];
        var result = new StringBuilder();
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
        {
            if (result.Length + count > maximum) throw new GenerationFailure("provider_output_too_large", false);
            result.Append(buffer, 0, count);
        }
        return result.ToString();
    }

    public void Dispose() => gate.Dispose();
    private sealed record EmbeddingResponse(string Model, float[] Vector);
    public sealed record IndexedChunk(int Page, string Text, float[] Vector);
    public sealed record IndexedDocument(string Model, int Pages, int PagesWithText, IndexedChunk[] Chunks);
}
