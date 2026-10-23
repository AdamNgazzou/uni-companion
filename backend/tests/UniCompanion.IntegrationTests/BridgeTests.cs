using System.Text.Json;
using Microsoft.Extensions.Configuration;
using UniCompanion.Infrastructure.Ai;
using UniCompanion.Core.Study;
using Xunit;

namespace UniCompanion.IntegrationTests;

public sealed class BridgeTests
{
    [Fact]
    public async Task MissingInterpreterDoesNotLeakTheProcessLease()
    {
        var root = Path.Combine(Path.GetTempPath(), "companion-bridge-" + Guid.NewGuid());
        try
        {
            using var bridge = Create(root, "/missing-companion-python", "/unused.py");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            for (var i = 0; i < 2; i++)
                await Assert.ThrowsAsync<AiUnavailableException>(() => bridge.SendAsync(new { prompt = "hello" }, TimeSpan.FromSeconds(1), timeout.Token));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public async Task InvalidStdoutAndWrongEmbeddingDimensionsAreRejected()
    {
        var root = Path.Combine(Path.GetTempPath(), "companion-bridge-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var script = Path.Combine(root, "fake_bridge.py");
            var python = Environment.GetEnvironmentVariable("Ai__Python") ?? throw new InvalidOperationException("Run through scripts/dev.py test.");
            using var bridge = Create(root, python, script);
            await File.WriteAllTextAsync(script, "import sys\nsys.stdin.readline()\nprint('not json')\n");
            await Assert.ThrowsAsync<AiUnavailableException>(() => bridge.SendAsync(new { prompt = "hello" }, TimeSpan.FromSeconds(3), CancellationToken.None));
            var response = JsonSerializer.Serialize(new { ok = true, model = PythonAiBridge.Model, vector = new[] { 1.0f } });
            await File.WriteAllTextAsync(script, "import sys\nsys.stdin.readline()\nprint(" + JsonSerializer.Serialize(response) + ")\n");
            await Assert.ThrowsAsync<AiUnavailableException>(() => bridge.EmbedAsync("hello", CancellationToken.None));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("{\"ok\":false,\"error\":\"provider_unavailable\",\"retryable\":true,\"retryAfter\":null}", true, null)]
    [InlineData("{\"ok\":false,\"error\":\"provider_unavailable\",\"retryable\":true,\"retryAfter\":17}", true, 17)]
    [InlineData("{\"ok\":false,\"error\":\"invalid_provider_response\",\"retryable\":false}", false, null)]
    [InlineData("{\"ok\":true,\"text\":\"\"}", true, null)]
    [InlineData("[]", false, null)]
    [InlineData("{not JSON}", false, null)]
    public async Task GenerationPreservesFailureClassification(string response, bool retryable, int? delay)
    {
        var root = Path.Combine(Path.GetTempPath(), "companion-generation-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            var script = Path.Combine(root, "fake_bridge.py");
            await File.WriteAllTextAsync(script, "import sys\nsys.stdin.readline()\nprint(" + JsonSerializer.Serialize(response) + ")\n");
            using var bridge = Create(root, Environment.GetEnvironmentVariable("Ai__Python")!, script);
            var failure = await Assert.ThrowsAsync<GenerationFailure>(() => bridge.GenerateAsync("test", CancellationToken.None));
            Assert.Equal(retryable, failure.Retryable); Assert.Equal(delay, failure.RetryAfter);
        }
        finally { Directory.Delete(root, true); }
    }

    private static PythonAiBridge Create(string root, string executable, string script) => new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Ai:Python"] = executable, ["Ai:Bridge"] = script, ["Storage:Root"] = root }).Build());
}
