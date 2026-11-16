using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniCompanion.Core.Community;
using UniCompanion.Core.Courses;
using UniCompanion.Core.Study;
using UniCompanion.Infrastructure.Activity;
using UniCompanion.Infrastructure.Ai;
using UniCompanion.Infrastructure.Persistence;
using UniCompanion.Infrastructure.Study;
using Xunit;

namespace UniCompanion.IntegrationTests;

public sealed class ActivityTests(ApiFixture app) : IClassFixture<ApiFixture>
{
    private static async Task<JsonElement> Create(HttpClient client, string path, object body, string? key = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        message.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N"));
        var response = await client.SendAsync(message); response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private async Task DrainAsync()
    {
        for (var i = 0; i < 100; i++)
        {
            await using var scope = app.Services.CreateAsyncScope();
            if (!await scope.ServiceProvider.GetRequiredService<ActivityDispatcher>().DispatchNextAsync(CancellationToken.None)) return;
        }
        throw new InvalidOperationException("Activity drain exceeded the bounded test backlog.");
    }
    private async Task<bool> DispatchAsync()
    {
        await using var scope = app.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ActivityDispatcher>().DispatchNextAsync(CancellationToken.None);
    }

    [Fact]
    public async Task AnonymousReplyCommitsOnceAndReadStateAndDestinationsAreOwnerBound()
    {
        await DrainAsync(); var owner = await app.RegisterAsync(); var actor = await app.RegisterAsync();
        using var writer = app.AsUser(owner.Access); using var responder = app.AsUser(actor.Access);
        var root = await Create(writer, "/api/v1/community/posts", new { title = "Activity thread", body = "Context" }); var rid = root.GetProperty("id").GetGuid();
        var key = Guid.NewGuid().ToString("N"); var request = new { body = "An anonymous reply", isAnonymous = true };
        var reply = await Create(responder, $"/api/v1/community/posts/{rid}/replies", request, key);
        await Create(responder, $"/api/v1/community/posts/{rid}/replies", request, key);
        var results = await Task.WhenAll(DispatchAsync(), DispatchAsync()); Assert.Single(results, value => value);
        var inbox = await writer.GetFromJsonAsync<JsonElement>("/api/v1/activity"); var items = inbox.GetProperty("data");
        Assert.Single(items.EnumerateArray()); Assert.Equal(1, inbox.GetProperty("unreadCount").GetInt32());
        Assert.Contains("Anonymous student", items[0].GetProperty("detail").GetString()); Assert.DoesNotContain("Test Student", items.ToString());
        Assert.False(items[0].TryGetProperty("actorId", out _));
        var itemId = items[0].GetProperty("id").GetGuid();
        Assert.Empty((await responder.GetFromJsonAsync<JsonElement>("/api/v1/activity")).GetProperty("data").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await responder.GetAsync($"/api/v1/activity/{itemId}/destination")).StatusCode);
        (await responder.PutAsync($"/api/v1/activity/{itemId}/read", null)).EnsureSuccessStatusCode();
        Assert.Equal(1, (await writer.GetFromJsonAsync<JsonElement>("/api/v1/activity/count")).GetProperty("count").GetInt32());
        var destination = await writer.GetFromJsonAsync<JsonElement>($"/api/v1/activity/{itemId}/destination");
        Assert.Equal(rid, destination.GetProperty("id").GetGuid()); Assert.Equal(reply.GetProperty("id").GetGuid(), destination.GetProperty("replyId").GetGuid());
        (await writer.PutAsync($"/api/v1/activity/{itemId}/read", null)).EnsureSuccessStatusCode();
        Assert.Equal(0, (await writer.GetFromJsonAsync<JsonElement>("/api/v1/activity/count")).GetProperty("count").GetInt32());
        (await responder.DeleteAsync($"/api/v1/community/posts/{reply.GetProperty("id").GetGuid()}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await writer.GetAsync($"/api/v1/activity/{itemId}/destination")).StatusCode);
    }

    [Fact]
    public async Task RollbackDoesNotPublishAndMutePreferencesAndBlocksAreRecheckedAtDispatch()
    {
        await DrainAsync(); var owner = await app.RegisterAsync(); var actor = await app.RegisterAsync();
        using var writer = app.AsUser(owner.Access); using var responder = app.AsUser(actor.Access);
        var root = await Create(writer, "/api/v1/community/posts", new { title = "Mutable subscriptions", body = "Context" }); var rootId = root.GetProperty("id").GetGuid();
        var me = await responder.GetFromJsonAsync<JsonElement>("/api/v1/accounts/me"); var actorId = me.GetProperty("id").GetString()!;
        var rolledBack = Guid.NewGuid();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await using var tx = await db.Database.BeginTransactionAsync();
            var post = new CommunityPost { Id = rolledBack, ParentId = rootId, AuthorId = actorId, Body = "Rolled back reply", RequestKey = Guid.NewGuid().ToString("N") };
            db.CommunityPosts.Add(post); await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<ActivityEvents>().ReplyAsync(post, CancellationToken.None); await tx.RollbackAsync();
        }
        await using (var scope = app.Services.CreateAsyncScope()) Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().ActivityEvents.AnyAsync(e => e.SourceId == rolledBack));
        await Create(responder, $"/api/v1/community/posts/{rootId}/replies", new { body = "Pending reply" });
        (await writer.PutAsJsonAsync($"/api/v1/community/posts/{rootId}/subscription", new { following = false })).EnsureSuccessStatusCode();
        await DrainAsync(); Assert.Equal(0, (await writer.GetFromJsonAsync<JsonElement>("/api/v1/activity/count")).GetProperty("count").GetInt32());
        (await writer.PutAsJsonAsync($"/api/v1/community/posts/{rootId}/subscription", new { following = true })).EnsureSuccessStatusCode();
        (await writer.PutAsJsonAsync("/api/v1/activity/preferences", new { replies = false })).EnsureSuccessStatusCode();
        await Create(responder, $"/api/v1/community/posts/{rootId}/replies", new { body = "Preference is off" }); await DrainAsync();
        Assert.Equal(0, (await writer.GetFromJsonAsync<JsonElement>("/api/v1/activity/count")).GetProperty("count").GetInt32());
        (await writer.PutAsJsonAsync("/api/v1/activity/preferences", new { replies = true })).EnsureSuccessStatusCode();
        var hidden = await Create(responder, $"/api/v1/community/posts/{rootId}/replies", new { body = "Anonymous participant", isAnonymous = true });
        (await writer.PostAsync($"/api/v1/community/posts/{hidden.GetProperty("id").GetGuid()}/block", null)).EnsureSuccessStatusCode();
        await DrainAsync(); Assert.Equal(0, (await writer.GetFromJsonAsync<JsonElement>("/api/v1/activity/count")).GetProperty("count").GetInt32());
        await Create(responder, $"/api/v1/community/posts/{rootId}/replies", new { body = "A named reply remains separate" }); await DrainAsync();
        Assert.Equal(1, (await writer.GetFromJsonAsync<JsonElement>("/api/v1/activity/count")).GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task GroupedUpvotesDoNotRevealVotersOrFarmUnreadThroughToggling()
    {
        await DrainAsync(); var owner = await app.RegisterAsync(); var first = await app.RegisterAsync(); var second = await app.RegisterAsync();
        using var writer = app.AsUser(owner.Access); using var voter = app.AsUser(first.Access); using var another = app.AsUser(second.Access);
        (await writer.PutAsJsonAsync("/api/v1/activity/preferences", new { upvotes = true })).EnsureSuccessStatusCode();
        var root = await Create(writer, "/api/v1/community/posts", new { title = "Vote activity", body = "Context" }); var id = root.GetProperty("id").GetGuid();
        (await writer.PutAsJsonAsync($"/api/v1/community/posts/{id}/vote", new { value = 1 })).EnsureSuccessStatusCode(); await DrainAsync();
        Assert.Equal(0, (await writer.GetFromJsonAsync<JsonElement>("/api/v1/activity/count")).GetProperty("count").GetInt32());
        (await voter.PutAsJsonAsync($"/api/v1/community/posts/{id}/vote", new { value = 1 })).EnsureSuccessStatusCode(); await DrainAsync();
        var inbox = await writer.GetFromJsonAsync<JsonElement>("/api/v1/activity"); Assert.Single(inbox.GetProperty("data").EnumerateArray());
        Assert.DoesNotContain("Test Student", inbox.GetProperty("data").ToString()); Assert.DoesNotContain(first.Email, inbox.ToString());
        (await writer.PutAsync("/api/v1/activity/read-all", null)).EnsureSuccessStatusCode();
        foreach (var value in new[] { 0, 1, -1, 1 }) (await voter.PutAsJsonAsync($"/api/v1/community/posts/{id}/vote", new { value })).EnsureSuccessStatusCode();
        await DrainAsync(); Assert.Equal(0, (await writer.GetFromJsonAsync<JsonElement>("/api/v1/activity/count")).GetProperty("count").GetInt32());
        (await another.PutAsJsonAsync($"/api/v1/community/posts/{id}/vote", new { value = 1 })).EnsureSuccessStatusCode(); await DrainAsync();
        inbox = await writer.GetFromJsonAsync<JsonElement>("/api/v1/activity"); Assert.Single(inbox.GetProperty("data").EnumerateArray()); Assert.Equal(1, inbox.GetProperty("unreadCount").GetInt32());
    }

    [Fact]
    public async Task SampleSelfAndDisabledRecipientsDoNotReceiveActivity()
    {
        await DrainAsync(); var owner = await app.RegisterAsync(); var actor = await app.RegisterAsync();
        using var writer = app.AsUser(owner.Access); using var responder = app.AsUser(actor.Access);
        var rootId = (await Create(writer, "/api/v1/community/posts", new { title = "Recipient checks", body = "Context" })).GetProperty("id").GetGuid();
        var ownerId = (await writer.GetFromJsonAsync<JsonElement>("/api/v1/accounts/me")).GetProperty("id").GetString()!;
        var actorId = (await responder.GetFromJsonAsync<JsonElement>("/api/v1/accounts/me")).GetProperty("id").GetString()!;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await using var tx = await db.Database.BeginTransactionAsync();
            var sample = new CommunityPost { ParentId = rootId, AuthorId = actorId, Body = "Sample reply", IsSample = true, RequestKey = Guid.NewGuid().ToString("N") };
            db.CommunityPosts.Add(sample); await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<ActivityEvents>().ReplyAsync(sample, CancellationToken.None);
            Assert.False(await db.ActivityEvents.AnyAsync(e => e.SourceId == sample.Id)); await tx.CommitAsync();
        }
        var replyId = (await Create(responder, $"/api/v1/community/posts/{rootId}/replies", new { body = "Before account disablement" })).GetProperty("id").GetGuid();
        await using (var scope = app.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.Where(u => u.Id == ownerId).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        await DrainAsync();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.False(await db.ActivityItems.AnyAsync(item => item.SourceId == replyId));
            await db.Users.Where(u => u.Id == ownerId).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, true));
        }
        var self = (await Create(writer, $"/api/v1/community/posts/{rootId}/replies", new { body = "My own follow-up" })).GetProperty("id").GetGuid();
        await DrainAsync();
        await using var final = app.Services.CreateAsyncScope();
        Assert.False(await final.ServiceProvider.GetRequiredService<AppDbContext>().ActivityItems.AnyAsync(item => item.SourceId == self && item.OwnerId == ownerId));
    }

    private async Task<Guid> StudyJobAsync(HttpClient client)
    {
        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/accounts/me");
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var course = new Course { Title = "Private study course" };
        var material = new Material { CourseId = course.Id, Title = "Introduction", FileName = "intro.pdf", SourceId = Guid.NewGuid().ToString(), ContentType = "application/pdf" };
        var version = new MaterialVersion { MaterialId = material.Id, Sha256 = new string('a', 64), PageCount = 1 };
        db.AddRange(course, material, version, new CourseMembership { CourseId = course.Id, UserId = me.GetProperty("id").GetString()! }); await db.SaveChangesAsync();
        material.CurrentVersionId = version.Id;
        db.DocumentChunks.Add(new DocumentChunk { MaterialVersionId = version.Id, Page = 1, Text = "Learning discovers patterns in data.", Model = PythonAiBridge.Model, Embedding = Enumerable.Repeat(0.1f, 384).ToArray() }); await db.SaveChangesAsync();
        return (await Create(client, $"/api/v1/courses/{course.Id}/study-jobs", new { kind = "summary", materialId = material.Id })).GetProperty("id").GetGuid();
    }
    private async Task RunStudyAsync()
    {
        await using var scope = app.Services.CreateAsyncScope(); Assert.True(await scope.ServiceProvider.GetRequiredService<StudyProcessor>().ProcessNextAsync(CancellationToken.None));
    }
    [Fact]
    public async Task ReplyDispatchContinuesWhileGenerationWaitsAndStudyPublicationQueuesOneEvent()
    {
        await DrainAsync(); var owner = await app.RegisterAsync(); var actor = await app.RegisterAsync();
        using var writer = app.AsUser(owner.Access); using var responder = app.AsUser(actor.Access);
        var jobId = await StudyJobAsync(writer); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Generation.Reply = async (prompt, token) => { entered.TrySetResult(); await release.Task.WaitAsync(token); return TestGeneration.Answer(prompt); };
        var running = RunStudyAsync();
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            var post = await Create(writer, "/api/v1/community/posts", new { title = "While I study", body = "Context" });
            await Create(responder, $"/api/v1/community/posts/{post.GetProperty("id").GetGuid()}/replies", new { body = "A reply during generation" });
            await DrainAsync(); Assert.False(running.IsCompleted);
            Assert.Equal(1, (await writer.GetFromJsonAsync<JsonElement>("/api/v1/activity/count")).GetProperty("count").GetInt32());
        }
        finally { release.TrySetResult(); await running; app.Generation.Reply = (prompt, _) => Task.FromResult(TestGeneration.Answer(prompt)); }
        await DrainAsync();
        var inbox = await writer.GetFromJsonAsync<JsonElement>("/api/v1/activity");
        var item = inbox.GetProperty("data").EnumerateArray().Single(row => row.GetProperty("kind").GetString() == "study_completed");
        Assert.Equal(jobId, (await writer.GetFromJsonAsync<JsonElement>($"/api/v1/activity/{item.GetProperty("id").GetGuid()}/destination")).GetProperty("id").GetGuid());
        await using var scope2 = app.Services.CreateAsyncScope(); var db2 = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db2.ActivityEvents.CountAsync(e => e.SourceId == jobId));
        var ownerId = (await writer.GetFromJsonAsync<JsonElement>("/api/v1/accounts/me")).GetProperty("id").GetString();
        await db2.CourseMemberships.Where(m => m.UserId == ownerId).ExecuteDeleteAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await writer.GetAsync($"/api/v1/activity/{item.GetProperty("id").GetGuid()}/destination")).StatusCode);
    }

    [Fact]
    public async Task RetryDoesNotNotifyBeforeTerminalFailure()
    {
        await DrainAsync(); var owner = await app.RegisterAsync(); using var writer = app.AsUser(owner.Access);
        var job = await StudyJobAsync(writer);
        app.Generation.Reply = (_, _) => throw new GenerationFailure("provider_busy", true, 1);
        try
        {
            await RunStudyAsync();
            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); Assert.False(await db.ActivityEvents.AnyAsync(e => e.SourceId == job));
                await db.StudyJobs.Where(j => j.Id == job).ExecuteUpdateAsync(s => s.SetProperty(j => j.Attempts, 2).SetProperty(j => j.NextAttemptAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
            }
            await RunStudyAsync(); await DrainAsync();
            Assert.Contains((await writer.GetFromJsonAsync<JsonElement>("/api/v1/activity")).GetProperty("data").EnumerateArray(), row => row.GetProperty("kind").GetString() == "study_failed");
        }
        finally { app.Generation.Reply = (prompt, _) => Task.FromResult(TestGeneration.Answer(prompt)); }
    }
}
