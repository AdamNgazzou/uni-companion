using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniCompanion.Core.Courses;
using UniCompanion.Core.Study;
using UniCompanion.Infrastructure.Ai;
using UniCompanion.Infrastructure.Persistence;
using UniCompanion.Infrastructure.Study;
using UniCompanion.Infrastructure.Storage;
using Xunit;

namespace UniCompanion.IntegrationTests;

public sealed class TestGeneration : ITextGeneration
{
    public Func<string, CancellationToken, Task<string>> Reply { get; set; } = (p, _) => Task.FromResult(Answer(p));
    public static string Answer(string prompt)
    {
        var tail = prompt.Split("EVIDENCE_JSON=")[1];
        var reader = new Utf8JsonReader(System.Text.Encoding.UTF8.GetBytes(tail));
        using var evidence = JsonDocument.ParseValue(ref reader);
        var source = evidence.RootElement[0];
        return JsonSerializer.Serialize(new { insufficientEvidence = false, paragraphs = new[] { new { text = "Learning discovers patterns in data.", citations = new[] { new { chunkId = source.GetProperty("chunkId").GetString(), quote = "Learning discovers patterns" } } } } });
    }
    public Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken) => Reply(prompt, cancellationToken);
}

public sealed class StudyTests(ApiFixture app) : IClassFixture<ApiFixture>
{
    private async Task<(HttpClient Client, Guid Course, Guid Material, Guid Version)> SeedAsync()
    {
        var user = await app.RegisterAsync();
        var client = app.AsUser(user.Access);
        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/accounts/me");
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var course = new Course { Title = "Machine Learning" };
        var material = new Material { CourseId = course.Id, Title = "Introduction", FileName = "intro.pdf", ContentType = "application/pdf", LessonId = "chapter-one", SourceId = Guid.NewGuid().ToString() };
        var version = new MaterialVersion { MaterialId = material.Id, Sha256 = new string('a', 64), PageCount = 1 };
        db.Add(course); db.Add(material); db.Add(version);
        db.Add(new CourseMembership { CourseId = course.Id, UserId = me.GetProperty("id").GetString()! });
        await db.SaveChangesAsync();
        material.CurrentVersionId = version.Id;
        db.Add(new DocumentChunk { MaterialVersionId = version.Id, Page = 1, Text = "Learning discovers patterns in data. Supervised learning uses labelled examples.", Model = PythonAiBridge.Model, Embedding = Enumerable.Repeat(0.1f, 384).ToArray() });
        await db.SaveChangesAsync();
        return (client, course.Id, material.Id, version.Id);
    }
    private static async Task<Guid> Enqueue(HttpClient client, Guid course, object request, string? key = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/courses/{course}/study-jobs") { Content = JsonContent.Create(request) };
        message.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N"));
        var response = await client.SendAsync(message);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    private async Task RunAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        Assert.True(await scope.ServiceProvider.GetRequiredService<StudyProcessor>().ProcessNextAsync(CancellationToken.None));
    }
    [Fact]
    public async Task SummaryPersistsCachesAndRemainsPrivate()
    {
        var (client, course, material, _) = await SeedAsync();
        using (client)
        {
            var key = Guid.NewGuid().ToString("N");
            var request = new { kind = "summary", materialId = material };
            var id = await Enqueue(client, course, request, key);
            Assert.Equal(id, await Enqueue(client, course, request, key));
            await RunAsync();
            var job = await client.GetFromJsonAsync<JsonElement>($"/api/v1/study-jobs/{id}");
            Assert.Equal("succeeded", job.GetProperty("state").GetString());
            Assert.Equal(1, job.GetProperty("result").GetProperty("coveredPages").GetInt32());
            Assert.Equal(id, await Enqueue(client, course, request));
            var stranger = await app.RegisterAsync();
            using var other = app.AsUser(stranger.Access);
            Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/study-artifacts/{id}")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.GetAsync($"/api/v1/study-jobs/{id}")).StatusCode);
        }
    }
    [Fact]
    public async Task RetryRecoversInNewScopeAndRevocationStopsPublication()
    {
        var (client, course, material, _) = await SeedAsync();
        using (client)
        {
            var id = await Enqueue(client, course, new { kind = "summary", materialId = material });
            app.Generation.Reply = (_, _) => throw new GenerationFailure("provider_busy", true, 1);
            await RunAsync();
            Assert.Equal("retry_wait", (await client.GetFromJsonAsync<JsonElement>($"/api/v1/study-jobs/{id}")).GetProperty("state").GetString());
            await using (var scope = app.Services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().StudyJobs.Where(j => j.Id == id).ExecuteUpdateAsync(s => s.SetProperty(j => j.NextAttemptAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
            app.Generation.Reply = (p, _) => Task.FromResult(TestGeneration.Answer(p));
            await RunAsync();
            Assert.Equal("succeeded", (await client.GetFromJsonAsync<JsonElement>($"/api/v1/study-jobs/{id}")).GetProperty("state").GetString());
            var second = await Enqueue(client, course, new { kind = "summary", chapterId = "chapter-one" });
            app.Generation.Reply = async (p, _) =>
            {
                await using var scope = app.Services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().CourseMemberships.Where(m => m.CourseId == course).ExecuteDeleteAsync();
                return TestGeneration.Answer(p);
            };
            await RunAsync();
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var job = await scope.ServiceProvider.GetRequiredService<AppDbContext>().StudyJobs.FindAsync(second);
                Assert.Equal("failed", job!.State); Assert.Null(job.ResultJson);
            }
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/study-jobs/{second}")).StatusCode);
            app.Generation.Reply = (p, _) => Task.FromResult(TestGeneration.Answer(p));
        }
    }
    [Fact]
    public async Task CancelledWorkerCannotPublishAndExpiredLeaseRecovers()
    {
        var (client, course, material, _) = await SeedAsync();
        using (client)
        {
            var id = await Enqueue(client, course, new { kind = "summary", materialId = material });
            await using (var scope = app.Services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().StudyJobs.Where(j => j.Id == id).ExecuteUpdateAsync(s => s.SetProperty(j => j.State, "running").SetProperty(j => j.Fence, 1).SetProperty(j => j.LeaseUntil, DateTimeOffset.UtcNow.AddMinutes(-1)));
            app.Generation.Reply = async (p, _) => { (await client.PostAsync($"/api/v1/study-jobs/{id}/cancel", null)).EnsureSuccessStatusCode(); return TestGeneration.Answer(p); };
            await RunAsync();
            var job = await client.GetFromJsonAsync<JsonElement>($"/api/v1/study-jobs/{id}");
            Assert.Equal("cancelled", job.GetProperty("state").GetString());
            Assert.Equal(JsonValueKind.Null, job.GetProperty("result").ValueKind);
            app.Generation.Reply = (p, _) => Task.FromResult(TestGeneration.Answer(p));
        }
    }
    [Fact]
    public async Task ConcurrentAdmissionAndConflictingKeysAreBounded()
    {
        var (client, course, _, _) = await SeedAsync();
        using (client)
        {
            var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(async i =>
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/courses/{course}/study-jobs") { Content = JsonContent.Create(new { kind = "answer", question = "Explain supervised learning " + i }) };
                message.Headers.Add("Idempotency-Key", "quota-" + i);
                return await client.SendAsync(message);
            }));
            Assert.Equal(2, results.Count(r => r.StatusCode == HttpStatusCode.Accepted));
            Assert.Equal(3, results.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests));
            foreach (var response in results.Where(r => r.StatusCode == HttpStatusCode.Accepted))
            {
                var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
                await client.PostAsync($"/api/v1/study-jobs/{id}/cancel", null);
            }
            var acceptedIndex = Array.FindIndex(results, r => r.StatusCode == HttpStatusCode.Accepted);
            using var mismatch = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/courses/{course}/study-jobs") { Content = JsonContent.Create(new { kind = "answer", question = "Different request" }) };
            mismatch.Headers.Add("Idempotency-Key", "quota-" + acceptedIndex);
            Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(mismatch)).StatusCode);
            foreach (var response in results) response.Dispose();
        }
    }
    [Fact]
    public async Task QuizKeysArePrivateAndGradesAreServerOwned()
    {
        var (client, course, material, version) = await SeedAsync();
        using (client)
        {
            var id = await Enqueue(client, course, new { kind = "quiz", materialId = material });
            var citation = new StudyCitation(Guid.NewGuid(), material, version, "intro.pdf", 1, "Learning discovers patterns");
            var quiz = new StudyResult("quiz", false, [], Enumerable.Range(0, 5).Select(i => new StudyQuestion("q" + i, "Question number " + i, [new("0", "Correct"), new("1", "Wrong"), new("2", "Other"), new("3", "Last")], "0", "Only the stored answer is correct.", [citation])).ToArray());
            await using (var scope = app.Services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().StudyJobs.Where(j => j.Id == id).ExecuteUpdateAsync(s => s.SetProperty(j => j.State, "succeeded").SetProperty(j => j.ResultJson, JsonSerializer.Serialize(quiz, StudyJobs.Json)));
            foreach (var path in new[] { "study-jobs", "study-artifacts" })
            {
                var json = await client.GetStringAsync($"/api/v1/{path}/{id}");
                Assert.DoesNotContain("correctOptionId", json); Assert.DoesNotContain("explanation", json);
            }
            async Task<HttpResponseMessage> Submit(object answers)
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/quizzes/{id}/attempts") { Content = JsonContent.Create(new { answers }) };
                message.Headers.Add("Idempotency-Key", "attempt-one"); return await client.SendAsync(message);
            }
            Assert.Equal(HttpStatusCode.BadRequest, (await Submit(new object?[] { null })).StatusCode);
            var answers = quiz.Questions.Select(q => new { questionId = q.Id, optionId = "1" }).ToArray();
            var first = await Submit(answers); Assert.True(first.IsSuccessStatusCode, await first.Content.ReadAsStringAsync());
            var result = await first.Content.ReadFromJsonAsync<JsonElement>(); Assert.Equal(0, result.GetProperty("correct").GetInt32());
            Assert.Equal(result.GetProperty("id").GetGuid(), (await (await Submit(answers)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
            Assert.Equal(HttpStatusCode.Conflict, (await Submit(quiz.Questions.Select(q => new { questionId = q.Id, optionId = "0" }))).StatusCode);
            Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/v1/quiz-attempts")).GetProperty("data").EnumerateArray());
        }
    }
    [Fact]
    public async Task ExpiredWorkerCannotOverwriteNewOwnersCompletedResult()
    {
        var (client, course, material, _) = await SeedAsync();
        using (client)
        {
            var id = await Enqueue(client, course, new { kind = "summary", materialId = material });
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            app.Generation.Reply = async (prompt, _) =>
            {
                if (Interlocked.Increment(ref calls) == 1) { entered.SetResult(); await release.Task; }
                return TestGeneration.Answer(prompt);
            };
            var previous = RunAsync();
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await using (var scope = app.Services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().StudyJobs.Where(j => j.Id == id).ExecuteUpdateAsync(s => s.SetProperty(j => j.LeaseUntil, DateTimeOffset.UtcNow.AddSeconds(-1)));
            await RunAsync();
            release.SetResult(); await previous;
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var job = await scope.ServiceProvider.GetRequiredService<AppDbContext>().StudyJobs.FindAsync(id);
                Assert.Equal("succeeded", job!.State); Assert.Equal(2, job.Fence); Assert.Equal(2, calls);
                Assert.Single(await scope.ServiceProvider.GetRequiredService<AppDbContext>().StudyCheckpoints.Where(c => c.JobId == id).ToListAsync());
            }
            app.Generation.Reply = (p, _) => Task.FromResult(TestGeneration.Answer(p));
        }
    }

    [Fact]
    public async Task GracefulShutdownLeavesJobRecoverableWithoutChargingExecutionBudget()
    {
        var (client, course, material, _) = await SeedAsync();
        using (client)
        {
            var id = await Enqueue(client, course, new { kind = "summary", materialId = material });
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            app.Generation.Reply = async (_, token) => { entered.SetResult(); await Task.Delay(Timeout.InfiniteTimeSpan, token); return ""; };
            using var shutdown = new CancellationTokenSource();
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var running = scope.ServiceProvider.GetRequiredService<StudyProcessor>().ProcessNextAsync(shutdown.Token);
                await entered.Task.WaitAsync(TimeSpan.FromSeconds(20)); shutdown.Cancel(); await running;
            }
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var job = await scope.ServiceProvider.GetRequiredService<AppDbContext>().StudyJobs.FindAsync(id);
                Assert.Equal("retry_wait", job!.State); Assert.Equal(0, job.Attempts);
            }
            app.Generation.Reply = (p, _) => Task.FromResult(TestGeneration.Answer(p));
            await RunAsync();
            Assert.Equal("succeeded", (await client.GetFromJsonAsync<JsonElement>($"/api/v1/study-jobs/{id}")).GetProperty("state").GetString());
        }
    }

    [Fact]
    public void QuizOptionIdsHideProviderPositionsAndKeepCorrectMapping()
    {
        var source = new StudySource(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "intro.pdf", 1, "Learning discovers patterns in data.");
        var json = JsonSerializer.Serialize(new { questions = Enumerable.Range(0, 5).Select(i => new { question = "What describes learning " + i + "?", options = new[] { "Correct statement", "Other concept", "Another concept", "Fourth concept" }, correctIndex = 0, explanation = "The cited source supports the correct statement.", citations = new[] { new { chunkId = source.ChunkId, quote = "Learning discovers patterns" } } }) });
        var quiz = StudyComposer.Parse(json, "quiz", "en", 5, [source]);
        foreach (var question in quiz.Questions)
        {
            Assert.All(question.Options, option => Assert.True(Guid.TryParseExact(option.Id, "N", out _)));
            Assert.Equal("Correct statement", question.Options.Single(option => option.Id == question.CorrectOptionId).Text);
        }
    }

    [Fact]
    public async Task HistoricalSourcesRequireMembershipAndStaleSnapshotsAreExplicit()
    {
        var (client, course, material, version) = await SeedAsync();
        using (client)
        {
            var id = await Enqueue(client, course, new { kind = "summary", materialId = material });
            await RunAsync();
            var sourcePath = $"/api/v1/materials/{material}/versions/{version}";
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var path = scope.ServiceProvider.GetRequiredService<PrivateFileStore>().PathFor(new string('a', 64));
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, "%PDF-1.4 fixture source bytes");
            }
            using var ranged = new HttpRequestMessage(HttpMethod.Get, sourcePath + "/content");
            ranged.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 4);
            var bytes = await client.SendAsync(ranged);
            Assert.Equal(HttpStatusCode.PartialContent, bytes.StatusCode);
            Assert.Equal("%PDF-", await bytes.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(sourcePath + "/pages/1")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(sourcePath + "/pages/2")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/materials/{material}/versions/{Guid.NewGuid()}/pages/1")).StatusCode);
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var replacement = new MaterialVersion { MaterialId = material, Sha256 = new string('b', 64), PageCount = 1 };
                db.MaterialVersions.Add(replacement); await db.SaveChangesAsync();
                await db.Materials.Where(m => m.Id == material).ExecuteUpdateAsync(s => s.SetProperty(m => m.CurrentVersionId, replacement.Id));
            }
            Assert.True((await client.GetFromJsonAsync<JsonElement>($"/api/v1/study-jobs/{id}")).GetProperty("stale").GetBoolean());
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(sourcePath + "/pages/1")).StatusCode);
            var other = await app.RegisterAsync(); using var stranger = app.AsUser(other.Access);
            Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(sourcePath + "/pages/1")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync(sourcePath + "/content")).StatusCode);
            using var denied = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/courses/{course}/study-jobs") { Content = JsonContent.Create(new { kind = "answer", question = "Explain this" }) };
            denied.Headers.Add("Idempotency-Key", "not-a-member");
            Assert.Equal(HttpStatusCode.NotFound, (await stranger.SendAsync(denied)).StatusCode);
        }
    }

    [Fact]
    public async Task BusinessErrorsPreserveProblemStatusAndMachineReadableCode()
    {
        var (client, course, _, _) = await SeedAsync();
        using (client)
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/courses/{course}/study-jobs") { Content = JsonContent.Create(new { kind = "unsupported" }) };
            message.Headers.Add("Idempotency-Key", "invalid-kind");
            var response = await client.SendAsync(message);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(400, problem.GetProperty("status").GetInt32());
            Assert.Equal("invalid_study_request", problem.GetProperty("title").GetString());
        }
    }

    [Fact]
    public async Task DailyAndGlobalPendingCeilingsRejectNewWork()
    {
        var (client, course, _, _) = await SeedAsync();
        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/accounts/me");
        var owner = me.GetProperty("id").GetString()!;
        var seedIds = new List<Guid>();
        try
        {
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                for (var i = 0; i < 50; i++)
                {
                    var job = new StudyJob { OwnerId = owner, CourseId = course, Kind = "answer", State = "failed", CreatedAt = DateTimeOffset.UtcNow.AddHours(-1) };
                    seedIds.Add(job.Id); db.StudyJobs.Add(job);
                }
                await db.SaveChangesAsync();
            }
            async Task<HttpStatusCode> Request(HttpClient caller)
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/courses/{course}/study-jobs") { Content = JsonContent.Create(new { kind = "answer", question = "Explain supervised learning" }) };
                message.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
                return (await caller.SendAsync(message)).StatusCode;
            }
            Assert.Equal(HttpStatusCode.TooManyRequests, await Request(client));
            var other = await app.RegisterAsync(); using var second = app.AsUser(other.Access);
            (await second.PostAsync($"/api/v1/courses/{course}/join", null)).EnsureSuccessStatusCode();
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var global = await db.StudyJobs.Where(j => seedIds.Contains(j.Id)).Take(20).ToArrayAsync();
                foreach (var job in global) job.State = "queued";
                await db.SaveChangesAsync();
            }
            Assert.Equal(HttpStatusCode.TooManyRequests, await Request(second));
        }
        finally
        {
            client.Dispose();
            await using var scope = app.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().StudyJobs.Where(j => seedIds.Contains(j.Id)).ExecuteDeleteAsync();
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("How learning works")]
    public void SummarySupportsHeadedNotesAndOlderArtifacts(string? heading)
    {
        var source = new StudySource(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "intro.pdf", 1, "Learning discovers patterns in data.");
        var json = JsonSerializer.Serialize(new { paragraphs = new[] { new { heading, text = "Learning discovers patterns.", citations = new[] { new { chunkId = source.ChunkId, quote = "Learning discovers patterns" } } } } });
        var result = StudyComposer.Parse(json, "summary", "en", 5, [source]);
        Assert.Equal(heading, Assert.Single(result.Paragraphs).Heading);
        Assert.Equal(source.ChunkId, Assert.Single(result.Paragraphs[0].Citations).ChunkId);
    }

    [Fact]
    public void ForgedCitationsCannotBecomeArtifacts()
    {
        var source = new StudySource(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "intro.pdf", 1, "Learning discovers patterns in data.");
        var forged = JsonSerializer.Serialize(new { paragraphs = new[] { new { text = "A claim", citations = new[] { new { chunkId = source.ChunkId, quote = "This quote is made up" } } } } });
        Assert.Throws<JsonException>(() => StudyComposer.Parse(forged, "answer", "en", 5, [source]));
    }
}
