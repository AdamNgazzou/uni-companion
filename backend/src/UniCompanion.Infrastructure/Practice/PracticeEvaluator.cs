using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniCompanion.Core.Practice;
using UniCompanion.Core.Study;
using UniCompanion.Infrastructure.Ai;
using UniCompanion.Infrastructure.Persistence;
using UniCompanion.Infrastructure.Study;

namespace UniCompanion.Infrastructure.Practice;

// Explicit local quality exercise. Does not execute generated code or create student records.
public sealed class PracticeEvaluator(AppDbContext db, PracticeComposer composer)
{
    public async Task RunAsync(string manifest, string output, CancellationToken ct, string[]? selectedCases = null)
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifest, ct));
        var reports = new List<object>();
        foreach (var test in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            if (selectedCases is not null && !selectedCases.Contains(test.GetProperty("id").GetString())) continue;
            var material = test.GetProperty("materialId").GetGuid();
            var sources = await (from chunk in db.DocumentChunks.AsNoTracking()
                                 join v in db.MaterialVersions on chunk.MaterialVersionId equals v.Id
                                 join m in db.Materials on v.MaterialId equals m.Id
                                 where m.Id == material && v.Id == m.CurrentVersionId && chunk.Model == PythonAiBridge.Model
                                 orderby chunk.Page, chunk.Ordinal
                                 select new StudySource(chunk.Id, m.Id, v.Id, m.FileName, chunk.Page, chunk.Text)).Take(24).ToArrayAsync(ct);
            if (sources.Length == 0) throw new InvalidOperationException("Pinned practice source is unavailable.");
            try
            {
                var response = 0;
                var artifact = await composer.ComposeAsync(new PracticeOptions(), test.GetProperty("language").GetString()!, test.GetProperty("topic").GetString(), sources, (_, _) => Task.CompletedTask, ct, text =>
                {
                    var path = Path.Combine(Path.GetDirectoryName(output)!, test.GetProperty("id").GetString() + "-raw-" + response++ + ".txt");
                    File.WriteAllText(path, text);
                    if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                });
                reports.Add(new { id = test.GetProperty("id").GetString(), state = "generated", sources, artifact });
                Console.WriteLine($"{test.GetProperty("id").GetString()}: generated; independent source/solution review required");
            }
            catch (GenerationFailure error)
            {
                reports.Add(new { id = test.GetProperty("id").GetString(), state = "failed", error = error.Code });
                Console.WriteLine($"{test.GetProperty("id").GetString()}: {error.Code}");
            }
            await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { version = PracticeComposer.Version, review = "Unreviewed: generation is not a quality score. Never execute without inspecting code.", reports }, new JsonSerializerOptions(StudyJobs.Json) { WriteIndented = true }), ct);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(output, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
