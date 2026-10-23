using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniCompanion.Core.Courses;
using UniCompanion.Core.Study;
using UniCompanion.Infrastructure.Identity;
using UniCompanion.Infrastructure.Persistence;

namespace UniCompanion.Infrastructure.Study;

// Explicitly invoked local quality exercise; never registered as an HTTP endpoint or startup job.
public sealed class StudyEvaluator(IServiceScopeFactory scopes)
{
    public async Task RunAsync(string manifest, string output, CancellationToken ct, string[]? selectedCases = null)
    {
        var owner = Guid.NewGuid().ToString();
        var reports = new List<object>();
        try
        {
            await using (var scope = scopes.CreateAsyncScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
                var user = new AppUser { Id = owner, UserName = $"study-evaluation-{owner}@example.test", Email = $"study-evaluation-{owner}@example.test", EmailConfirmed = true };
                var created = await users.CreateAsync(user);
                if (!created.Succeeded) throw new InvalidOperationException("Could not create isolated evaluation account.");
            }
            using var gold = JsonDocument.Parse(await File.ReadAllTextAsync(manifest, ct));
            foreach (var item in gold.RootElement.GetProperty("cases").EnumerateArray())
            {
                if (selectedCases is not null && !selectedCases.Contains(item.GetProperty("id").GetString())) continue;
                if (item.GetProperty("kind").GetString() == "access") continue; // Covered by authenticated integration tests.
                var source = item.GetProperty("courseSourceId").GetString();
                Guid courseId;
                await using (var scope = scopes.CreateAsyncScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    courseId = await db.Courses.Where(c => c.SourceId == source).Select(c => c.Id).SingleAsync(ct);
                    if (!await db.CourseMemberships.AnyAsync(m => m.CourseId == courseId && m.UserId == owner, ct))
                    {
                        db.CourseMemberships.Add(new CourseMembership { CourseId = courseId, UserId = owner });
                        await db.SaveChangesAsync(ct);
                    }
                }
                var question = item.GetProperty("question").GetString()!;
                var french = question.StartsWith("Qu", StringComparison.Ordinal) || question.StartsWith("Comment", StringComparison.Ordinal) || question.StartsWith("Pourquoi", StringComparison.Ordinal) || question.StartsWith("Dans", StringComparison.Ordinal);
                var job = await RunJobAsync(courseId, owner, new StudyRequest("answer", question, Language: french ? "fr" : "en"), ct);
                reports.Add(new { id = item.GetProperty("id").GetString(), kind = item.GetProperty("kind").GetString(), job.State, job.ErrorCode, job.Attempts, job.ProviderCalls, result = job.ResultJson is null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(job.ResultJson) });
                Console.WriteLine($"{item.GetProperty("id").GetString()}: {job.State}; calls={job.ProviderCalls}");
                await SaveAsync();
            }
            if (selectedCases is not null && !selectedCases.Any(id => id.StartsWith("quiz-", StringComparison.Ordinal) || id.StartsWith("summary-", StringComparison.Ordinal))) return;
            Guid ml;
            Material material;
            await using (var scope = scopes.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                ml = await db.Courses.Where(c => c.SourceId == "7f28c669-7912-4f2b-a5dd-d3a94f04763f").Select(c => c.Id).SingleAsync(ct);
                if (!await db.CourseMemberships.AnyAsync(m => m.CourseId == ml && m.UserId == owner, ct))
                { db.CourseMemberships.Add(new CourseMembership { CourseId = ml, UserId = owner }); await db.SaveChangesAsync(ct); }
                material = await db.Materials.AsNoTracking().FirstAsync(m => m.CourseId == ml && m.FileName.StartsWith("980aaba5"), ct);
            }
            foreach (var (id, request) in new[] {
                ("summary-ml", new StudyRequest("summary", MaterialId: material.Id)),
                ("summary-chapter", new StudyRequest("summary", ChapterId: material.LessonId)),
                ("quiz-en-5", new StudyRequest("quiz", MaterialId: material.Id, Count: 5)),
                ("quiz-fr-10", new StudyRequest("quiz", MaterialId: material.Id, Language: "fr", Count: 10)) })
            {
                if (selectedCases is not null && !selectedCases.Contains(id)) continue;
                var job = await RunJobAsync(ml, owner, request, ct);
                reports.Add(new { id, kind = request.Kind, job.State, job.ErrorCode, job.Attempts, job.ProviderCalls, result = job.ResultJson is null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(job.ResultJson) });
                Console.WriteLine($"{id}: {job.State}; calls={job.ProviderCalls}");
                await SaveAsync();
            }
            async Task SaveAsync()
            {
                await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new { generatedAt = DateTimeOffset.UtcNow, promptVersion = StudyJobs.PromptVersion, review = "Source review required; successful generation is not a quality score.", reports }, new JsonSerializerOptions(StudyJobs.Json) { WriteIndented = true }), ct);
                if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(output, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
        }
        finally
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Users.Where(u => u.Id == owner).ExecuteDeleteAsync(CancellationToken.None);
        }
    }
    private async Task<StudyJob> RunJobAsync(Guid course, string owner, StudyRequest request, CancellationToken ct)
    {
        Guid id;
        var key = Guid.NewGuid().ToString("N");
        while (true)
        {
            await using var scope = scopes.CreateAsyncScope();
            try { id = (await scope.ServiceProvider.GetRequiredService<StudyJobs>().EnqueueAsync(course, owner, key, request, ct)).Id; break; }
            catch (StudyRequestException e) when (e.Status == 429) { await Task.Delay(TimeSpan.FromSeconds(15), ct); }
        }
        var deadline = DateTimeOffset.UtcNow.AddMinutes(11);
        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(1), ct);
            await using var scope = scopes.CreateAsyncScope();
            var job = await scope.ServiceProvider.GetRequiredService<StudyJobs>().GetAsync(id, owner, ct);
            if (!JobStates.IsPending(job.State)) return job;
        }
        throw new TimeoutException("The study worker did not finish. Check that the separate worker is running.");
    }
}
