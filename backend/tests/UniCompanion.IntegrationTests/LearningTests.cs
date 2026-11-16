using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniCompanion.Core.Courses;
using UniCompanion.Core.Learning;
using UniCompanion.Core.Practice;
using UniCompanion.Core.Study;
using UniCompanion.Infrastructure.Learning;
using UniCompanion.Infrastructure.Persistence;
using UniCompanion.Infrastructure.Study;
using Xunit;

namespace UniCompanion.IntegrationTests;

public sealed class LearningTests(ApiFixture app) : IClassFixture<ApiFixture>
{
    private async Task<(HttpClient Client, string Owner, Guid Course)> SeedAsync()
    {
        var user = await app.RegisterAsync(); var client = app.AsUser(user.Access);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/accounts/me"); var owner = me.GetProperty("id").GetString()!;
        var response = await client.PostAsJsonAsync("/api/v1/courses/", new { title = "Continuity special course", description = "" }); response.EnsureSuccessStatusCode();
        var course = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid(); return (client, owner, course);
    }
    private static async Task<HttpResponseMessage> Send(HttpClient client, string method, string path, object body, string? key = null)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), "/api/v1" + path) { Content = JsonContent.Create(body) };
        if (key is not null) request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }
    [Fact]
    public async Task HistoryExcludesChatBeforePaginationAndSearchHonorsOwnerAndLiveMembership()
    {
        var (client, owner, course) = await SeedAsync(); using var writer = client;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.StudyJobs.Add(new StudyJob { OwnerId = owner, CourseId = course, Kind = "summary", State = JobStates.Succeeded, CreatedAt = DateTimeOffset.UtcNow.AddDays(-3) });
            for (var i = 0; i < 105; i++) db.StudyJobs.Add(new StudyJob { OwnerId = owner, CourseId = course, Kind = "tutor" });
            await db.SaveChangesAsync();
        }
        var history = await writer.GetFromJsonAsync<JsonElement>("/api/v1/study-jobs"); Assert.Single(history.GetProperty("data").EnumerateArray()); Assert.Equal(1, history.GetProperty("total").GetInt32());
        var search = await writer.GetFromJsonAsync<JsonElement>("/api/v1/study-jobs?q=special&limit=1&page=1"); Assert.Single(search.GetProperty("data").EnumerateArray());
        Assert.Empty((await writer.GetFromJsonAsync<JsonElement>("/api/v1/study-jobs?q=%25")).GetProperty("data").EnumerateArray());
        Assert.Equal(100, (await writer.GetFromJsonAsync<JsonElement>("/api/v1/study-jobs?kind=tutor")).GetProperty("data").GetArrayLength());
        var stranger = await app.RegisterAsync(); using var other = app.AsUser(stranger.Access); Assert.Empty((await other.GetFromJsonAsync<JsonElement>("/api/v1/study-jobs?q=special")).GetProperty("data").EnumerateArray());
        await using (var scope = app.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<AppDbContext>().CourseMemberships.Where(m => m.CourseId == course && m.UserId == owner).ExecuteDeleteAsync();
        Assert.Empty((await writer.GetFromJsonAsync<JsonElement>("/api/v1/study-jobs?q=special")).GetProperty("data").EnumerateArray());
    }
    [Fact]
    public async Task LatestQuizAttemptDrivesRevisionAndSavedLabelsSnoozeAndConceptReplayArePrivate()
    {
        var (client, owner, course) = await SeedAsync(); using var writer = client;
        var job = new StudyJob { OwnerId = owner, CourseId = course, Kind = "quiz", State = JobStates.Succeeded };
        var first = new QuizAttempt { OwnerId = owner, JobId = job.Id, Key = "first", ResultJson = Corrections(false), CreatedAt = DateTimeOffset.UtcNow.AddHours(-1) };
        await using (var scope = app.Services.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.Add(job); db.Add(first); await db.SaveChangesAsync(); }
        var queue = await writer.GetFromJsonAsync<JsonElement>("/api/v1/learning/revision"); var item = Assert.Single(queue.GetProperty("data").EnumerateArray()); var key = item.GetProperty("key").GetString()!;
        var edit = new RevisionUpdate("My corrected label", "Decision trees", "Compare inputs and outputs", "snoozed", 1);
        (await Send(writer, "PUT", "/learning/revision/" + key, edit)).EnsureSuccessStatusCode();
        var snoozed = Assert.Single((await writer.GetFromJsonAsync<JsonElement>("/api/v1/learning/revision?state=snoozed")).GetProperty("data").EnumerateArray()); var until = snoozed.GetProperty("snoozedUntil").GetString();
        (await Send(writer, "PUT", "/learning/revision/" + key, edit with { Note = "New note", SnoozeDays = null })).EnsureSuccessStatusCode();
        Assert.Equal(until, (await writer.GetFromJsonAsync<JsonElement>("/api/v1/learning/revision?state=snoozed")).GetProperty("data")[0].GetProperty("snoozedUntil").GetString());
        var stranger = await app.RegisterAsync(); using var other = app.AsUser(stranger.Access);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(other, "PUT", "/learning/revision/" + key, edit)).StatusCode);
        await using (var scope = app.Services.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.QuizAttempts.Add(new QuizAttempt { OwnerId = owner, JobId = job.Id, Key = "second", ResultJson = Corrections(true), CreatedAt = DateTimeOffset.UtcNow }); await db.SaveChangesAsync(); }
        Assert.Empty((await writer.GetFromJsonAsync<JsonElement>("/api/v1/learning/revision?state=snoozed")).GetProperty("data").EnumerateArray());
        var concept = new ConceptRequest("Explain a mapping", "My topic", "My private note");
        var created = await Send(writer, "POST", "/learning/revision", concept, "concept-key"); created.EnsureSuccessStatusCode(); var conceptKey = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("key").GetString()!;
        (await Send(writer, "PUT", "/learning/revision/" + conceptKey, new RevisionUpdate("Edited title", "", "", "dismissed"))).EnsureSuccessStatusCode();
        (await Send(writer, "POST", "/learning/revision", concept, "concept-key")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await Send(writer, "POST", "/learning/revision", concept with { Title = "Changed" }, "concept-key")).StatusCode);
        Assert.Single((await writer.GetFromJsonAsync<JsonElement>("/api/v1/learning/revision?state=dismissed&q=edited")).GetProperty("data").EnumerateArray());
        Assert.Empty((await other.GetFromJsonAsync<JsonElement>("/api/v1/learning/revision?state=dismissed")).GetProperty("data").EnumerateArray());
    }
    private static string Corrections(bool correct) => JsonSerializer.Serialize(new { corrections = new[] { new { questionId = "question", question = "Explain a labelled mapping", selectedOptionId = correct ? "right" : "wrong", correctOptionId = "right" } } }, StudyJobs.Json);
    [Fact]
    public async Task CountsSeparateAttemptsAssistanceAndSelfCompletionAndHideRevokedCourseWork()
    {
        var (client, owner, course) = await SeedAsync(); using var writer = client;
        var task = new PracticeTask("lab", "coding", "Lab", "Count labels", "Synthetic labels", [], "Count", [], [], null, null, null, null, [], []);
        var job = new StudyJob { OwnerId = owner, CourseId = course, Kind = "practice", State = JobStates.Succeeded, ResultJson = JsonSerializer.Serialize(new StudyResult("practice", false, [], [], Practice: new PracticeArtifact("coding", "en", [], [], [task])), StudyJobs.Json) };
        await using (var scope = app.Services.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.Add(job); await db.SaveChangesAsync(); }
        Assert.Equal(HttpStatusCode.Conflict, (await Send(writer, "PUT", $"/practice/{job.Id}/tasks/lab/completion", new { completed = true })).StatusCode);
        (await Send(writer, "POST", $"/practice/{job.Id}/tasks/lab/attempts", new { answer = "Code as text" }, "attempt1")).EnsureSuccessStatusCode();
        (await Send(writer, "POST", $"/practice/{job.Id}/tasks/lab/attempts", new { answer = "Revised code" }, "attempt2")).EnsureSuccessStatusCode();
        var counts = await writer.GetFromJsonAsync<JsonElement>("/api/v1/learning/progress"); Assert.Equal(1, counts.GetProperty("codingLabsAttempted").GetInt32()); Assert.Equal(2, counts.GetProperty("practiceAttempts").GetInt32()); Assert.Equal(1, counts.GetProperty("assistedTasks").GetInt32()); Assert.Equal(0, counts.GetProperty("selfCompletedTasks").GetInt32());
        (await Send(writer, "PUT", $"/practice/{job.Id}/tasks/lab/completion", new { completed = true })).EnsureSuccessStatusCode();
        (await Send(writer, "PUT", $"/practice/{job.Id}/tasks/lab/completion", new { completed = true })).EnsureSuccessStatusCode();
        Assert.Equal(1, (await writer.GetFromJsonAsync<JsonElement>("/api/v1/learning/progress")).GetProperty("selfCompletedTasks").GetInt32());
        (await Send(writer, "PUT", $"/practice/{job.Id}/tasks/lab/completion", new { completed = false })).EnsureSuccessStatusCode();
        Assert.Equal(0, (await writer.GetFromJsonAsync<JsonElement>("/api/v1/learning/progress")).GetProperty("selfCompletedTasks").GetInt32());
        await using (var scope = app.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<AppDbContext>().CourseMemberships.Where(m => m.CourseId == course && m.UserId == owner).ExecuteDeleteAsync();
        var revoked = await writer.GetFromJsonAsync<JsonElement>("/api/v1/learning/progress"); Assert.Equal(0, revoked.GetProperty("practiceAttempts").GetInt32()); Assert.Equal(0, revoked.GetProperty("savedSessions").GetInt32());
    }
    [Fact]
    public void ReminderClockHandlesTimezoneQuietHoursSpringGapAndFallFold()
    {
        var daily = new ReminderRequest(true, 18 * 60, "Africa/Lagos");
        Assert.Equal(DateTimeOffset.Parse("2026-10-24T17:00:00Z"), ReminderTimes.Next(DateTimeOffset.Parse("2026-10-24T16:00:00Z"), daily));
        Assert.Equal(DateTimeOffset.Parse("2026-10-25T17:00:00Z"), ReminderTimes.Next(DateTimeOffset.Parse("2026-10-24T17:00:00Z"), daily));
        Assert.Throws<StudyRequestException>(() => ReminderTimes.Validate(daily with { MinuteOfDay = 23 * 60 }));
        Assert.Throws<StudyRequestException>(() => ReminderTimes.Validate(daily with { TimeZoneId = "Fake/Place" }));
        var gap = new ReminderRequest(true, 150, "America/New_York", 0, 0);
        Assert.Equal(DateTimeOffset.Parse("2026-03-08T07:00:00Z"), ReminderTimes.Next(DateTimeOffset.Parse("2026-03-08T05:00:00Z"), gap));
        var fold = gap with { MinuteOfDay = 90 };
        var first = ReminderTimes.Next(DateTimeOffset.Parse("2026-11-01T04:00:00Z"), fold); Assert.Equal(DateTimeOffset.Parse("2026-11-01T05:30:00Z"), first);
        Assert.Equal(DateTimeOffset.Parse("2026-11-02T06:30:00Z"), ReminderTimes.Next(first, fold));
        Assert.True(ReminderTimes.IsQuiet(30, 1320, 420)); Assert.False(ReminderTimes.IsQuiet(420, 1320, 420));
    }
    [Fact]
    public async Task DueRemindersCommitOnceAcrossDispatchersAndOptOutStopsFutureDelivery()
    {
        var (client, owner, _) = await SeedAsync(); using var writer = client;
        var request = new ReminderRequest(true, 12 * 60, "UTC", 0, 0);
        (await Send(writer, "PUT", "/learning/reminder", request)).EnsureSuccessStatusCode();
        await using (var scope = app.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<AppDbContext>().StudyReminders.Where(r => r.OwnerId == owner).ExecuteUpdateAsync(s => s.SetProperty(r => r.NextDueAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        async Task<bool> Dispatch() { await using var scope = app.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<RevisionReminders>().DispatchNextAsync(CancellationToken.None); }
        Assert.Single(await Task.WhenAll(Dispatch(), Dispatch()), value => value);
        var activity = await writer.GetFromJsonAsync<JsonElement>("/api/v1/activity"); var item = Assert.Single(activity.GetProperty("data").EnumerateArray()); Assert.Equal("revision_reminder", item.GetProperty("kind").GetString());
        var id = item.GetProperty("id").GetGuid(); Assert.Equal("revision", (await writer.GetFromJsonAsync<JsonElement>($"/api/v1/activity/{id}/destination")).GetProperty("kind").GetString());
        var stranger = await app.RegisterAsync(); using var other = app.AsUser(stranger.Access); Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/activity/{id}/destination")).StatusCode);
        (await Send(writer, "PUT", "/learning/reminder", request with { Enabled = false })).EnsureSuccessStatusCode(); Assert.False(await Dispatch());
        Assert.False((await writer.GetFromJsonAsync<JsonElement>("/api/v1/learning/reminder")).GetProperty("enabled").GetBoolean());
    }
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    [Fact]
    public async Task DelayedReminderSkipsQuietHoursAndInactiveAccounts()
    {
        var (client, owner, _) = await SeedAsync(); using var writer = client;
        (await Send(writer, "PUT", "/learning/reminder", new ReminderRequest(true, 18 * 60, "UTC", 12 * 60, 14 * 60))).EnsureSuccessStatusCode();
        var now = DateTimeOffset.Parse("2026-10-24T13:00:00Z");
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.StudyReminders.Where(r => r.OwnerId == owner).ExecuteUpdateAsync(s => s.SetProperty(r => r.NextDueAt, now.AddHours(-2)));
        Assert.True(await new RevisionReminders(db, new FixedClock(now)).DispatchNextAsync(CancellationToken.None));
        Assert.False(await db.ActivityItems.AnyAsync(i => i.OwnerId == owner));
        db.ChangeTracker.Clear();
        await db.Users.Where(u => u.Id == owner).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        await db.StudyReminders.Where(r => r.OwnerId == owner).ExecuteUpdateAsync(s => s.SetProperty(r => r.NextDueAt, now));
        Assert.True(await new RevisionReminders(db, new FixedClock(now.AddHours(2))).DispatchNextAsync(CancellationToken.None));
        Assert.False(await db.ActivityItems.AnyAsync(i => i.OwnerId == owner));
    }
}
