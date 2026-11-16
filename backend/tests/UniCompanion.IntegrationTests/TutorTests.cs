using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniCompanion.Core.Courses;
using UniCompanion.Core.Practice;
using UniCompanion.Core.Study;
using UniCompanion.Core.Tutor;
using UniCompanion.Infrastructure.Ai;
using UniCompanion.Infrastructure.Persistence;
using UniCompanion.Infrastructure.Study;
using Xunit;

namespace UniCompanion.IntegrationTests;

public sealed class TutorTests(ApiFixture app) : IClassFixture<ApiFixture>
{
    private async Task<(HttpClient Client, string Owner, Guid Course, Guid Material, Guid Version)> SeedAsync()
    {
        app.Generation.Reply = (prompt, _) => Task.FromResult(TestGeneration.Answer(prompt));
        var account = await app.RegisterAsync(); var client = app.AsUser(account.Access);
        var owner = (await client.GetFromJsonAsync<JsonElement>("/api/v1/accounts/me")).GetProperty("id").GetString()!;
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var course = new Course { Title = "Tutor course" };
        var material = new Material { CourseId = course.Id, Title = "Introduction", ContentType = "application/pdf", FileName = "intro.pdf", LessonId = "chapter", SourceId = Guid.NewGuid().ToString() };
        var version = new MaterialVersion { MaterialId = material.Id, Sha256 = new string('a', 64), PageCount = 1 };
        db.AddRange(course, material, version, new CourseMembership { CourseId = course.Id, UserId = owner }); await db.SaveChangesAsync();
        material.CurrentVersionId = version.Id;
        db.DocumentChunks.Add(new DocumentChunk { MaterialVersionId = version.Id, Page = 1, Text = "Learning discovers patterns in data. Supervised learning uses labelled examples.", Model = PythonAiBridge.Model, Embedding = Enumerable.Repeat(0.1f, 384).ToArray() }); await db.SaveChangesAsync();
        return (client, owner, course.Id, material.Id, version.Id);
    }
    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object? body, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1" + path);
        if (body is not null) request.Content = JsonContent.Create(body);
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N")); return await client.SendAsync(request);
    }
    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync()); return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static async Task<Guid> Conversation(HttpClient client, Guid course, Guid material) => (await Json(await Post(client, $"/courses/{course}/tutor-conversations", new { materialId = material }))).GetProperty("id").GetGuid();
    private async Task ProcessAsync()
    {
        await using var scope = app.Services.CreateAsyncScope(); Assert.True(await scope.ServiceProvider.GetRequiredService<StudyProcessor>().ProcessNextAsync(CancellationToken.None));
    }
    private async Task<Guid> NextVersion(Guid material)
    {
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var version = new MaterialVersion { MaterialId = material, Sha256 = new string('b', 64), PageCount = 1 }; db.MaterialVersions.Add(version); await db.SaveChangesAsync();
        await db.Materials.Where(m => m.Id == material).ExecuteUpdateAsync(s => s.SetProperty(m => m.CurrentVersionId, version.Id));
        db.DocumentChunks.Add(new DocumentChunk { MaterialVersionId = version.Id, Page = 1, Text = "Learning discovers patterns from fresh authorized source evidence.", Model = PythonAiBridge.Model, Embedding = Enumerable.Repeat(0.1f, 384).ToArray() }); await db.SaveChangesAsync(); return version.Id;
    }
    [Fact]
    public async Task TurnsAreAtomicIdempotentSerializedAndOwnerBoundAndPublicJobsRejectForgedContext()
    {
        var (client, _, course, material, _) = await SeedAsync(); using var writer = client;
        var created = await Task.WhenAll(Post(writer, $"/courses/{course}/tutor-conversations", new { materialId = material }, "create-one"), Post(writer, $"/courses/{course}/tutor-conversations", new { materialId = material }, "create-one"));
        var id = (await Json(created[0])).GetProperty("id").GetGuid(); Assert.Equal(id, (await Json(created[1])).GetProperty("id").GetGuid());
        var sent = await Task.WhenAll(Post(writer, $"/tutor/conversations/{id}/turns", new { question = "Explain supervised learning" }, "turn-one"), Post(writer, $"/tutor/conversations/{id}/turns", new { question = "Explain supervised learning" }, "turn-one"));
        var first = await Json(sent[0]); var replay = await Json(sent[1]); Assert.Equal(first.GetProperty("jobId").GetGuid(), replay.GetProperty("jobId").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, (await Post(writer, $"/tutor/conversations/{id}/turns", new { question = "Different payload" }, "turn-one")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(writer, $"/tutor/conversations/{id}/turns", new { question = "Concurrent next turn" })).StatusCode);
        var otherAccount = await app.RegisterAsync(); using var other = app.AsUser(otherAccount.Access);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/tutor/conversations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(other, $"/tutor/conversations/{id}/turns", new { question = "Why?" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/v1/tutor/conversations/{id}")).StatusCode);
        var jobId = first.GetProperty("jobId").GetGuid(); var job = await writer.GetFromJsonAsync<JsonElement>($"/api/v1/study-jobs/{jobId}");
        Assert.Equal(id, job.GetProperty("request").GetProperty("conversationId").GetGuid()); Assert.False(job.GetProperty("request").TryGetProperty("tutor", out _));
        foreach (var kind in new[] { "tutor", "answer" })
            Assert.Equal(HttpStatusCode.BadRequest, (await Post(writer, $"/courses/{course}/study-jobs", new { kind, question = "Forged context", tutor = new { conversationId = id, ordinal = 1, action = "check", history = Array.Empty<object>() } })).StatusCode);
        var state = await writer.GetFromJsonAsync<JsonElement>($"/api/v1/tutor/conversations/{id}"); Assert.Single(state.GetProperty("turns").EnumerateArray());
        (await writer.DeleteAsync($"/api/v1/tutor/conversations/{id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await writer.GetAsync($"/api/v1/study-jobs/{jobId}")).StatusCode);
    }
    [Fact]
    public async Task WhyFollowupCarriesPreviousTopicButRetrievesNewSourceVersionsAndDoesNotSpamActivity()
    {
        var (client, _, course, material, oldVersion) = await SeedAsync(); using var writer = client;
        var id = await Conversation(writer, course, material); var prompts = new List<string>();
        app.Generation.Reply = (prompt, _) => { prompts.Add(prompt); return Task.FromResult(TestGeneration.Answer(prompt)); };
        var first = await Json(await Post(writer, $"/tutor/conversations/{id}/turns", new { question = "Explain supervised learning" })); await ProcessAsync();
        var newVersion = await NextVersion(material);
        var second = await Json(await Post(writer, $"/tutor/conversations/{id}/turns", new { question = "Why?", action = "explain" })); var jobId = second.GetProperty("jobId").GetGuid();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var stored = await db.StudyJobs.SingleAsync(j => j.Id == jobId);
            var input = JsonSerializer.Deserialize<StudyRequest>(stored.RequestJson, StudyJobs.Json)!;
            Assert.Single(input.Tutor!.History); Assert.Equal("Explain supervised learning", input.Tutor.History[0].Question); Assert.Contains("Learning discovers", input.Tutor.History[0].AnswerExcerpt);
            Assert.Equal(new[] { newVersion }, stored.SourceVersionIds); Assert.DoesNotContain(oldVersion, stored.SourceVersionIds);
        }
        await ProcessAsync(); Assert.Equal(2, prompts.Count); Assert.Contains("RECENT_CONVERSATION_EXCERPTS_JSON", prompts[1]); Assert.Contains("Explain supervised learning", prompts[1]); Assert.Contains("fresh authorized source evidence", prompts[1]);
        var conversation = await writer.GetFromJsonAsync<JsonElement>($"/api/v1/tutor/conversations/{id}"); var turns = conversation.GetProperty("turns"); Assert.Equal(2, turns.GetArrayLength()); Assert.Equal(2, turns[1].GetProperty("ordinal").GetInt32());
        Assert.Equal(newVersion, turns[1].GetProperty("result").GetProperty("paragraphs")[0].GetProperty("citations")[0].GetProperty("versionId").GetGuid());
        await using var final = app.Services.CreateAsyncScope(); var database = final.ServiceProvider.GetRequiredService<AppDbContext>();
        var ids = new[] { first.GetProperty("jobId").GetGuid(), jobId }; Assert.False(await database.ActivityEvents.AnyAsync(e => ids.Contains(e.SourceId)));
    }
    [Fact]
    public async Task TemporaryRetrievalFailureRetriesTheSameTurnWithoutCallingTheGeneratorOrDuplicatingHistory()
    {
        var (client, _, course, material, _) = await SeedAsync(); using var writer = client; var id = await Conversation(writer, course, material);
        var sent = await Json(await Post(writer, $"/tutor/conversations/{id}/turns", new { question = "Why do supervised models need labels?" })); var jobId = sent.GetProperty("jobId").GetGuid();
        var embeddings = (FakeEmbedding)app.Services.GetRequiredService<IQueryEmbedding>(); embeddings.FailuresRemaining = 1;
        await ProcessAsync();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var job = await db.StudyJobs.SingleAsync(j => j.Id == jobId);
            Assert.Equal(JobStates.Retry, job.State); Assert.Equal("retrieval_unavailable", job.ErrorCode); Assert.Equal(0, job.ProviderCalls); Assert.Null(job.ResultJson);
            Assert.Equal(HttpStatusCode.Conflict, (await Post(writer, $"/tutor/conversations/{id}/turns", new { question = "A second simultaneous question" })).StatusCode);
            await db.StudyJobs.Where(j => j.Id == jobId).ExecuteUpdateAsync(s => s.SetProperty(j => j.NextAttemptAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
        }
        await ProcessAsync();
        var conversation = await writer.GetFromJsonAsync<JsonElement>($"/api/v1/tutor/conversations/{id}"); Assert.Single(conversation.GetProperty("turns").EnumerateArray()); Assert.Equal(JobStates.Succeeded, conversation.GetProperty("turns")[0].GetProperty("state").GetString());
        await using var final = app.Services.CreateAsyncScope(); var result = await final.ServiceProvider.GetRequiredService<AppDbContext>().StudyJobs.SingleAsync(j => j.Id == jobId); Assert.Equal(2, result.Attempts); Assert.Equal(1, result.ProviderCalls);
    }
    [Fact]
    public async Task FailedReplyKeepsTheSentQuestionAndAllowsANewTurnWithoutInventedHistory()
    {
        var (client, _, course, material, _) = await SeedAsync(); using var writer = client; var id = await Conversation(writer, course, material);
        app.Generation.Reply = (_, _) => throw new GenerationFailure("provider_unavailable", false);
        await Json(await Post(writer, $"/tutor/conversations/{id}/turns", new { question = "Explain learning" })); await ProcessAsync();
        var failed = await writer.GetFromJsonAsync<JsonElement>($"/api/v1/tutor/conversations/{id}");
        Assert.Equal("Explain learning", failed.GetProperty("turns")[0].GetProperty("question").GetString()); Assert.Equal(JobStates.Failed, failed.GetProperty("turns")[0].GetProperty("state").GetString()); Assert.Equal(JsonValueKind.Null, failed.GetProperty("turns")[0].GetProperty("result").ValueKind);
        var retry = await Json(await Post(writer, $"/tutor/conversations/{id}/turns", new { question = "Explain learning" })); var jobId = retry.GetProperty("jobId").GetGuid();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var job = await scope.ServiceProvider.GetRequiredService<AppDbContext>().StudyJobs.SingleAsync(j => j.Id == jobId);
            Assert.Empty(JsonSerializer.Deserialize<StudyRequest>(job.RequestJson, StudyJobs.Json)!.Tutor!.History);
        }
        app.Generation.Reply = (prompt, _) => Task.FromResult(TestGeneration.Answer(prompt)); await ProcessAsync();
        var done = await writer.GetFromJsonAsync<JsonElement>($"/api/v1/tutor/conversations/{id}"); Assert.Equal(2, done.GetProperty("turns").GetArrayLength()); Assert.Equal(JobStates.Succeeded, done.GetProperty("turns")[1].GetProperty("state").GetString());
    }
    private async Task<(Guid Job, Guid Attempt)> PracticeAttemptAsync(HttpClient client, string owner, Guid course, Guid material, Guid version)
    {
        var task = new PracticeTask("task", "written", "Reasoning", "Explain learning", "Synthetic context", ["A hint"], "PRIVATE_REFERENCE_SOLUTION", ["PRIVATE_CRITERION"], [], null, null, null, null, [], []);
        var artifact = new StudyResult("practice", false, [], [], Practice: new PracticeArtifact("written", "en", ["Explain"], [], [task]));
        var job = new StudyJob
        {
            OwnerId = owner,
            CourseId = course,
            Kind = "practice",
            State = JobStates.Succeeded,
            SourceVersionIds = [version],
            CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
            RequestJson = JsonSerializer.Serialize(new StudyRequest("practice", MaterialId: material, Practice: new PracticeOptions("written")), StudyJobs.Json),
            ResultJson = JsonSerializer.Serialize(artifact, StudyJobs.Json)
        };
        await using (var scope = app.Services.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.StudyJobs.Add(job); await db.SaveChangesAsync(); }
        var attempt = await Json(await Post(client, $"/practice/{job.Id}/tasks/task/attempts", new { answer = "My saved private reasoning" }));
        return (job.Id, attempt.GetProperty("id").GetGuid());
    }
    [Fact]
    public async Task FeedbackUsesOwnedSubmittedWorkAndOriginalSourcesWithoutReturningPrivateGenerationContext()
    {
        var (client, owner, course, material, version) = await SeedAsync(); using var writer = client;
        Assert.Equal(HttpStatusCode.NotFound, (await Post(writer, $"/practice/{Guid.NewGuid()}/attempts/{Guid.NewGuid()}/feedback", null)).StatusCode);
        var practice = await PracticeAttemptAsync(writer, owner, course, material, version); await NextVersion(material);
        var created = await Json(await Post(writer, $"/practice/{practice.Job}/attempts/{practice.Attempt}/feedback", null, "feedback-one")); var id = created.GetProperty("id").GetGuid();
        Assert.Equal(id, (await Json(await Post(writer, $"/practice/{practice.Job}/attempts/{practice.Attempt}/feedback", null, "feedback-one"))).GetProperty("id").GetGuid());
        var otherAccount = await app.RegisterAsync(); using var other = app.AsUser(otherAccount.Access);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(other, $"/practice/{practice.Job}/attempts/{practice.Attempt}/feedback", null)).StatusCode);
        var conversation = await writer.GetFromJsonAsync<JsonElement>($"/api/v1/tutor/conversations/{id}"); var jobId = conversation.GetProperty("turns")[0].GetProperty("jobId").GetGuid();
        Assert.Equal(practice.Job, conversation.GetProperty("practice").GetProperty("jobId").GetGuid()); Assert.True(conversation.GetProperty("practice").GetProperty("usesEarlierSources").GetBoolean());
        foreach (var path in new[] { $"/study-jobs/{jobId}", "/study-jobs", $"/tutor/conversations/{id}" }) Assert.DoesNotContain("PRIVATE_", await writer.GetStringAsync("/api/v1" + path));
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var job = await scope.ServiceProvider.GetRequiredService<AppDbContext>().StudyJobs.SingleAsync(j => j.Id == jobId);
            var input = JsonSerializer.Deserialize<StudyRequest>(job.RequestJson, StudyJobs.Json)!;
            Assert.Equal("PRIVATE_REFERENCE_SOLUTION", input.Tutor!.Feedback!.Solution); Assert.Equal("My saved private reasoning", input.Tutor.Feedback.Answer); Assert.Contains("PRIVATE_CRITERION", input.Tutor.Feedback.Rubric); Assert.Equal(new[] { version }, job.SourceVersionIds);
        }
        app.Generation.Reply = (prompt, _) =>
        {
            Assert.Contains("TASK=feedback", prompt); Assert.Contains("PRIVATE_CRITERION", prompt);
            return Task.FromResult("""{"insufficientEvidence":false,"paragraphs":[{"text":"Your saved reasoning needs one concrete example; compare it with the given task.","basis":"saved_work","citations":[]}]}""");
        };
        await ProcessAsync(); var done = await writer.GetFromJsonAsync<JsonElement>($"/api/v1/study-jobs/{jobId}"); Assert.Equal("succeeded", done.GetProperty("state").GetString());
        Assert.Equal("saved_work", done.GetProperty("result").GetProperty("paragraphs")[0].GetProperty("basis").GetString());
        Assert.Empty(done.GetProperty("result").GetProperty("paragraphs")[0].GetProperty("citations").EnumerateArray());
        app.Generation.Reply = (prompt, _) => Task.FromResult(TestGeneration.Answer(prompt));
        (await writer.DeleteAsync($"/api/v1/tutor/conversations/{id}")).EnsureSuccessStatusCode();
        await using var final = app.Services.CreateAsyncScope(); var db = final.ServiceProvider.GetRequiredService<AppDbContext>(); Assert.True(await db.PracticeAttempts.AnyAsync(a => a.Id == practice.Attempt)); Assert.True(await db.StudyJobs.AnyAsync(j => j.Id == practice.Job)); Assert.False(await db.StudyJobs.AnyAsync(j => j.Id == jobId));
    }
    [Fact]
    public void SavedWorkAttributionIsLimitedToFeedbackAndCannotReplaceRequiredCourseCitations()
    {
        var work = """{"paragraphs":[{"text":"Your code always returns null; try counting the supplied labels first.","basis":"saved_work","citations":[]}]}""";
        var result = StudyComposer.Parse(work, "feedback", "en", 5, []);
        Assert.Equal("saved_work", Assert.Single(result.Paragraphs).Basis);
        foreach (var kind in new[] { "answer", "summary" }) Assert.Throws<JsonException>(() => StudyComposer.Parse(work, kind, "en", 5, []));
        Assert.Throws<JsonException>(() => StudyComposer.Parse(work.Replace("saved_work", "course_sources"), "feedback", "en", 5, []));
        Assert.Throws<JsonException>(() => StudyComposer.Parse(work.Replace("saved_work", "unknown"), "feedback", "en", 5, []));
        var source = new StudySource(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "intro.pdf", 1, "Learning discovers patterns in data.");
        var cited = TestGeneration.Answer("EVIDENCE_JSON=" + JsonSerializer.Serialize(new[] { new { source.ChunkId, source.Text } }, StudyJobs.Json));
        Assert.Throws<KeyNotFoundException>(() => StudyComposer.Parse(cited, "feedback", "en", 5, [source]));
        var course = cited.Replace("\"text\":", "\"basis\":\"course_sources\",\"text\":");
        Assert.Single(StudyComposer.Parse(course, "feedback", "en", 5, [source]).Paragraphs[0].Citations);
        Assert.Throws<JsonException>(() => StudyComposer.Parse(course.Replace("course_sources", "saved_work"), "feedback", "en", 5, [source]));
    }
    [Fact]
    public async Task RevocationHidesConversationsAndStopsPublicationWhileClearHistoryRemainsOwnerBound()
    {
        var (client, owner, course, material, _) = await SeedAsync(); using var writer = client;
        var id = await Conversation(writer, course, material); var turn = await Json(await Post(writer, $"/tutor/conversations/{id}/turns", new { question = "Explain learning" })); var jobId = turn.GetProperty("jobId").GetGuid();
        var otherAccount = await app.RegisterAsync(); using var other = app.AsUser(otherAccount.Access); (await other.DeleteAsync("/api/v1/tutor/conversations")).EnsureSuccessStatusCode();
        await using (var scope = app.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<AppDbContext>().CourseMemberships.Where(m => m.CourseId == course && m.UserId == owner).ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await writer.GetAsync($"/api/v1/tutor/conversations/{id}")).StatusCode); Assert.Empty((await writer.GetFromJsonAsync<JsonElement>("/api/v1/tutor/conversations")).GetProperty("data").EnumerateArray());
        await ProcessAsync();
        await using (var scope = app.Services.CreateAsyncScope()) Assert.Equal(JobStates.Failed, (await scope.ServiceProvider.GetRequiredService<AppDbContext>().StudyJobs.SingleAsync(j => j.Id == jobId)).State);
        (await writer.DeleteAsync("/api/v1/tutor/conversations")).EnsureSuccessStatusCode();
        await using var final = app.Services.CreateAsyncScope(); Assert.False(await final.ServiceProvider.GetRequiredService<AppDbContext>().StudyJobs.AnyAsync(j => j.Id == jobId));
    }
    [Fact]
    public async Task QuotaFailureRollsBackFeedbackConversationAndFirstTurnAdmission()
    {
        var (client, owner, course, material, version) = await SeedAsync(); using var writer = client; var practice = await PracticeAttemptAsync(writer, owner, course, material, version);
        for (var i = 0; i < 2; i++) await Json(await Post(writer, $"/courses/{course}/study-jobs", new { kind = "summary", materialId = material }));
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Post(writer, $"/practice/{practice.Job}/attempts/{practice.Attempt}/feedback", null)).StatusCode);
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.TutorConversations.AnyAsync(c => c.OwnerId == owner)); Assert.False(await db.TutorTurns.AnyAsync(t => t.OwnerId == owner)); Assert.Equal(3, await db.StudyJobs.CountAsync(j => j.OwnerId == owner));
        await db.StudyJobs.Where(j => j.OwnerId == owner && j.State == JobStates.Queued).ExecuteUpdateAsync(s => s.SetProperty(j => j.State, JobStates.Cancelled));
    }
    [Fact]
    public async Task ContextIsBoundedToThreeRecentPairsAndConversationCapacityIsEnforced()
    {
        var (client, owner, course, material, version) = await SeedAsync(); using var writer = client; var id = await Conversation(writer, course, material);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 1; i <= 6; i++)
            {
                var job = new StudyJob
                {
                    OwnerId = owner,
                    CourseId = course,
                    Kind = "tutor",
                    State = JobStates.Succeeded,
                    CreatedAt = DateTimeOffset.UtcNow.AddHours(-1),
                    SourceVersionIds = [version],
                    RequestJson = JsonSerializer.Serialize(new StudyRequest("tutor", "Topic " + i, MaterialId: material), StudyJobs.Json),
                    ResultJson = JsonSerializer.Serialize(new StudyResult("tutor", false, [new StudyParagraph(new string('x', 2500), [])], []), StudyJobs.Json)
                };
                db.StudyJobs.Add(job); db.TutorTurns.Add(new TutorTurn { ConversationId = id, OwnerId = owner, Ordinal = i, Question = "Topic " + i, JobId = job.Id, RequestKey = "seed-" + i });
            }
            await db.SaveChangesAsync();
        }
        var sent = await Json(await Post(writer, $"/tutor/conversations/{id}/turns", new { question = "Why?" }));
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var jobId = sent.GetProperty("jobId").GetGuid(); var job = await db.StudyJobs.SingleAsync(j => j.Id == jobId);
            var input = JsonSerializer.Deserialize<StudyRequest>(job.RequestJson, StudyJobs.Json)!;
            Assert.Equal(3, input.Tutor!.History.Length); Assert.Equal("Topic 4", input.Tutor.History[0].Question); Assert.All(input.Tutor.History, pair => Assert.Equal(1800, pair.AnswerExcerpt.Length));
            await db.StudyJobs.Where(j => j.Id == jobId).ExecuteUpdateAsync(s => s.SetProperty(j => j.State, JobStates.Cancelled));
            for (var i = 8; i <= 40; i++)
            {
                var historical = new StudyJob { OwnerId = owner, CourseId = course, Kind = "tutor", State = JobStates.Failed, CreatedAt = DateTimeOffset.UtcNow.AddHours(-1) }; db.StudyJobs.Add(historical);
                db.TutorTurns.Add(new TutorTurn { ConversationId = id, OwnerId = owner, Ordinal = i, Question = "Historical", JobId = historical.Id, RequestKey = "seed-" + i });
            }
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await Post(writer, $"/tutor/conversations/{id}/turns", new { question = "Beyond the limit" })).StatusCode);
        Assert.Equal(40, (await writer.GetFromJsonAsync<JsonElement>($"/api/v1/tutor/conversations/{id}")).GetProperty("turns").GetArrayLength());
        (await writer.DeleteAsync($"/api/v1/tutor/conversations/{id}")).EnsureSuccessStatusCode();
    }
}
