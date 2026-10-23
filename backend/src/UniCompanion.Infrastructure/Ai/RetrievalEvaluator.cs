using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniCompanion.Infrastructure.Persistence;

namespace UniCompanion.Infrastructure.Ai;

public sealed class RetrievalEvaluator(AppDbContext db, CourseSearch search)
{
    public async Task<bool> EvaluateAsync(string path, CancellationToken ct = default)
    {
        using var file = JsonDocument.Parse(await File.ReadAllTextAsync(path, ct));
        var passed = 0;
        var total = 0;
        foreach (var item in file.RootElement.GetProperty("cases").EnumerateArray())
        {
            if (item.GetProperty("kind").GetString() != "answerable") continue;
            var sourceId = item.GetProperty("courseSourceId").GetString();
            var courseId = await db.Courses.Where(c => c.SourceId == sourceId).Select(c => c.Id).SingleAsync(ct);
            // Trusted local evaluation only: deliberately search as admin; public HTTP always checks membership.
            var hits = await search.SearchAsync(courseId, "", true, item.GetProperty("question").GetString()!, ct);
            var success = hits.Take(5).Any(hit => item.GetProperty("expected").EnumerateArray().Any(expected =>
                hit.FileName.StartsWith(expected.GetProperty("filePrefix").GetString()!, StringComparison.Ordinal) && hit.Page == expected.GetProperty("page").GetInt32()));
            total++;
            if (success) passed++;
            Console.WriteLine($"{item.GetProperty("id").GetString()}: {(success ? "PASS" : "MISS")}; top5={string.Join(',', hits.Take(5).Select(h => h.FileName[..8] + ":" + h.Page))}");
        }
        Console.WriteLine($"Expected-source recall@5: {passed}/{total}; required {file.RootElement.GetProperty("targetTop5").GetInt32()}/{total}. Insufficient-evidence generation checks remain a separate phase.");
        return passed >= file.RootElement.GetProperty("targetTop5").GetInt32();
    }
}
