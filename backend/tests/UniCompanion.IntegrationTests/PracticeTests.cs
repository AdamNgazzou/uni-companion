using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniCompanion.Core.Courses;
using UniCompanion.Core.Practice;
using UniCompanion.Core.Study;
using UniCompanion.Infrastructure.Persistence;
using UniCompanion.Infrastructure.Study;
using Xunit;

namespace UniCompanion.IntegrationTests;

public sealed class PracticeTests(ApiFixture app) : IClassFixture<ApiFixture>
{
    private async Task<(HttpClient Client, Guid Job, Guid Course, string Owner)> SeedAsync()
    {
        var account = await app.RegisterAsync(); var client = app.AsUser(account.Access);
        var owner = (await client.GetFromJsonAsync<JsonElement>("/api/v1/accounts/me")).GetProperty("id").GetString()!;
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var course = new Course { Title = "Practice course" }; db.Courses.Add(course);
        db.CourseMemberships.Add(new CourseMembership { CourseId = course.Id, UserId = owner });
        var task = new PracticeTask("task", "written", "Generated task", "Explain map", "Synthetic word cat", ["PRIVATE_HINT_ONE", "PRIVATE_HINT_TWO"], "PRIVATE_SOLUTION", ["PRIVATE_RUBRIC"], [], null, null, null, null, [], []);
        var result = new StudyResult("practice", false, [], [], Practice: new PracticeArtifact("written", "en", ["Map words"], [], [task]));
        var job = new StudyJob
        {
            OwnerId = owner,
            CourseId = course.Id,
            Kind = "practice",
            State = JobStates.Succeeded,
            RequestJson = JsonSerializer.Serialize(new StudyRequest("practice", ChapterId: "chapter", Practice: new PracticeOptions("written")), StudyJobs.Json),
            ResultJson = JsonSerializer.Serialize(result, StudyJobs.Json)
        };
        db.StudyJobs.Add(job); await db.SaveChangesAsync(); return (client, job.Id, course.Id, owner);
    }
    [Fact]
    public async Task NewPracticeRequestsCreateFreshSetsWhileRetriesKeepTheirOriginalJob()
    {
        var (client, _, course, _) = await SeedAsync(); using var writer = client;
        var material = new Material { CourseId = course, ContentType = "application/pdf", FileName = "notes.pdf", Title = "Notes", SourceId = Guid.NewGuid().ToString() };
        var version = new MaterialVersion { MaterialId = material.Id, Sha256 = new string('b', 64), PageCount = 1 };
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.AddRange(material, version); await db.SaveChangesAsync();
            material.CurrentVersionId = version.Id;
            db.DocumentChunks.Add(new DocumentChunk { MaterialVersionId = version.Id, Page = 1, Text = "Classification predicts a category from labelled examples.", Model = UniCompanion.Infrastructure.Ai.PythonAiBridge.Model, Embedding = Enumerable.Repeat(0.1f, 384).ToArray() }); await db.SaveChangesAsync();
        }
        async Task<HttpResponseMessage> Enqueue(string key, object? options = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/courses/{course}/study-jobs") { Content = JsonContent.Create(new { kind = "practice", materialId = material.Id, practice = options ?? new { mode = "mixed", difficulty = "guided", minutes = 30 } }) };
            request.Headers.Add("Idempotency-Key", key); return await writer.SendAsync(request);
        }
        var first = await Enqueue("first-practice"); first.EnsureSuccessStatusCode();
        var id = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await using (var scope = app.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().StudyJobs.Where(j => j.Id == id).ExecuteUpdateAsync(s => s.SetProperty(j => j.State, JobStates.Succeeded));
        var replay = await Enqueue("first-practice"); replay.EnsureSuccessStatusCode();
        Assert.Equal(id, (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        var fresh = await Enqueue("fresh-practice"); fresh.EnsureSuccessStatusCode();
        Assert.NotEqual(id, (await fresh.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.BadRequest, (await Enqueue("invalid", new { mode = "mixed", difficulty = "guided", minutes = 999 })).StatusCode);
    }
    [Fact]
    public async Task SolutionsRubricsAndFutureHintsStayPrivateUntilExplicitOwnerActions()
    {
        var (client, job, course, owner) = await SeedAsync(); using var writer = client;
        foreach (var path in new[] { $"/study-jobs/{job}", $"/study-artifacts/{job}", $"/practice/{job}/progress" })
        {
            var text = await writer.GetStringAsync("/api/v1" + path); Assert.DoesNotContain("PRIVATE_", text);
        }
        var other = await app.RegisterAsync(); using var stranger = app.AsUser(other.Access);
        foreach (var path in new[] { $"/practice/{job}/progress", $"/study-jobs/{job}" }) Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync("/api/v1" + path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PutAsync($"/api/v1/practice/{job}/tasks/task/solution", null)).StatusCode);
        (await writer.PutAsJsonAsync($"/api/v1/practice/{job}/tasks/task/hint", new { level = 1 })).EnsureSuccessStatusCode();
        var hints = await writer.GetStringAsync($"/api/v1/practice/{job}/progress"); Assert.Contains("PRIVATE_HINT_ONE", hints); Assert.DoesNotContain("PRIVATE_HINT_TWO", hints); Assert.DoesNotContain("PRIVATE_SOLUTION", hints);
        (await writer.PutAsync($"/api/v1/practice/{job}/tasks/task/solution", null)).EnsureSuccessStatusCode();
        var revealed = await writer.GetStringAsync($"/api/v1/practice/{job}/progress"); Assert.Contains("PRIVATE_SOLUTION", revealed); Assert.Contains("PRIVATE_RUBRIC", revealed);
        // Generic job reads must remain private even after a reveal.
        Assert.DoesNotContain("PRIVATE_", await writer.GetStringAsync($"/api/v1/study-jobs/{job}"));
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.CourseMemberships.Where(m => m.CourseId == course && m.UserId == owner).ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await writer.GetAsync($"/api/v1/practice/{job}/progress")).StatusCode);
    }
    [Fact]
    public async Task AttemptsPersistOnceAndRecordAssistanceBeforeRevealingWithoutExecutingCode()
    {
        var (client, job, _, _) = await SeedAsync(); using var writer = client;
        async Task<HttpResponseMessage> Submit(string text, string key = "submission-one")
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/practice/{job}/tasks/task/attempts") { Content = JsonContent.Create(new { answer = text }) };
            request.Headers.Add("Idempotency-Key", key); return await writer.SendAsync(request);
        }
        (await writer.PutAsJsonAsync($"/api/v1/practice/{job}/tasks/task/hint", new { level = 1 })).EnsureSuccessStatusCode();
        var submissions = await Task.WhenAll(Submit("print('pasted code is text only')"), Submit("print('pasted code is text only')"));
        var first = submissions[0]; var replay = submissions[1]; first.EnsureSuccessStatusCode(); replay.EnsureSuccessStatusCode();
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await Submit("different")).StatusCode);
        var state = await writer.GetFromJsonAsync<JsonElement>($"/api/v1/practice/{job}/progress"); var task = state.GetProperty("tasks")[0]; var attempts = task.GetProperty("attempts");
        Assert.Single(attempts.EnumerateArray()); Assert.True(attempts[0].GetProperty("hintsUsed").GetBoolean()); Assert.False(attempts[0].GetProperty("solutionViewedBefore").GetBoolean());
        Assert.Equal("PRIVATE_SOLUTION", task.GetProperty("solution").GetString());
        (await Submit("Second attempt", "submission-two")).EnsureSuccessStatusCode();
        state = await writer.GetFromJsonAsync<JsonElement>($"/api/v1/practice/{job}/progress");
        Assert.True(state.GetProperty("tasks")[0].GetProperty("attempts")[0].GetProperty("solutionViewedBefore").GetBoolean());
    }
}
