using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniCompanion.Infrastructure.Community;
using UniCompanion.Infrastructure.Persistence;
using Xunit;

namespace UniCompanion.IntegrationTests;

public sealed class CommunityTests(ApiFixture app) : IClassFixture<ApiFixture>
{
    private static async Task<JsonElement> Create(HttpClient client, string path = "/api/v1/community/posts", object? body = null, string? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body ?? new { title = "A useful question", body = "A little context", kind = "Question" }) };
        request.Headers.Add("Idempotency-Key", key ?? "test-" + Guid.NewGuid().ToString("N"));
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    [Fact]
    public async Task AnonymousThreadAndOriginalAuthorReplyNeverExposeIdentity()
    {
        var author = await app.RegisterAsync(); var reader = await app.RegisterAsync();
        using var writer = app.AsUser(author.Access); using var viewer = app.AsUser(reader.Access);
        var post = await Create(writer, body: new { title = "Can someone explain?", body = "Trying to learn", isAnonymous = true });
        var id = post.GetProperty("id").GetGuid();
        await Create(writer, $"/api/v1/community/posts/{id}/replies", new { body = "A follow-up from the same author", isAnonymous = true });
        var root = await viewer.GetFromJsonAsync<JsonElement>($"/api/v1/community/posts/{id}");
        Assert.Equal("Anonymous student", root.GetProperty("authorName").GetString());
        Assert.False(root.GetProperty("isMine").GetBoolean());
        Assert.False(root.TryGetProperty("authorId", out _));
        Assert.DoesNotContain(author.Email, root.GetRawText());
        var replies = await viewer.GetFromJsonAsync<JsonElement>($"/api/v1/community/posts/{id}/replies");
        var reply = Assert.Single(replies.GetProperty("data").EnumerateArray());
        Assert.Equal("Anonymous student", reply.GetProperty("authorName").GetString());
        Assert.True(reply.GetProperty("isOriginalPoster").GetBoolean());
        Assert.DoesNotContain("Test Student", reply.GetRawText());
        // OP markers must not link anonymous content to a named profile through a mixed-mode reply.
        var namedReply = await Create(writer, $"/api/v1/community/posts/{id}/replies", new { body = "A named reply", isAnonymous = false });
        Assert.False(namedReply.GetProperty("isOriginalPoster").GetBoolean());
        var namedRoot = await Create(writer);
        var namedId = namedRoot.GetProperty("id").GetGuid();
        var hiddenReply = await Create(writer, $"/api/v1/community/posts/{namedId}/replies", new { body = "An anonymous reply", isAnonymous = true });
        Assert.False(hiddenReply.GetProperty("isOriginalPoster").GetBoolean());
        Assert.Equal("Anonymous student", hiddenReply.GetProperty("authorName").GetString());
        var feed = await viewer.GetFromJsonAsync<JsonElement>("/api/v1/community/posts?sort=top&kind=Discussion");
        Assert.DoesNotContain(author.Email, feed.GetRawText());
    }
    [Fact]
    public async Task VotesAreAtomicSetsAndCanFlipOrBeRemoved()
    {
        var user = await app.RegisterAsync(); using var client = app.AsUser(user.Access);
        var post = await Create(client); var id = post.GetProperty("id").GetGuid();
        var path = $"/api/v1/community/posts/{id}/vote";
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => client.PutAsJsonAsync(path, new { value = 1 })));
        Assert.All(responses, response => response.EnsureSuccessStatusCode());
        var voted = await client.GetFromJsonAsync<JsonElement>($"/api/v1/community/posts/{id}");
        Assert.Equal(1, voted.GetProperty("score").GetInt32()); Assert.Equal(1, voted.GetProperty("myVote").GetInt32());
        var flipped = await (await client.PutAsJsonAsync(path, new { value = -1 })).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(-1, flipped.GetProperty("score").GetInt32());
        (await client.PutAsJsonAsync(path, new { value = 0 })).EnsureSuccessStatusCode();
        var cleared = await client.GetFromJsonAsync<JsonElement>($"/api/v1/community/posts/{id}");
        Assert.Equal(0, cleared.GetProperty("score").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(path, new { value = 2 })).StatusCode);
    }
    [Fact]
    public async Task CreationIsRepeatSafeValidatedAndDeletionRequiresOwnership()
    {
        var owner = await app.RegisterAsync(); var other = await app.RegisterAsync();
        using var writer = app.AsUser(owner.Access); using var stranger = app.AsUser(other.Access);
        var key = Guid.NewGuid().ToString("N");
        var simultaneous = await Task.WhenAll(Create(writer, key: key), Create(writer, key: key));
        var first = simultaneous[0]; var again = simultaneous[1];
        Assert.Equal(first.GetProperty("id").GetGuid(), again.GetProperty("id").GetGuid());
        var id = first.GetProperty("id").GetGuid();
        using var changed = new HttpRequestMessage(HttpMethod.Post, "/api/v1/community/posts") { Content = JsonContent.Create(new { title = "Changed", body = "Different" }) };
        changed.Headers.Add("Idempotency-Key", key);
        Assert.Equal(HttpStatusCode.Conflict, (await writer.SendAsync(changed)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await writer.PostAsJsonAsync("/api/v1/community/posts", new { title = " ", body = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await writer.PostAsJsonAsync("/api/v1/community/posts", new { title = "Title", body = new string('x', 8001) })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/api/v1/community/posts/{id}")).StatusCode);
        var reply = await Create(stranger, $"/api/v1/community/posts/{id}/replies", new { body = "A reply" });
        var replyId = reply.GetProperty("id").GetGuid();
        using var nested = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/community/posts/{replyId}/replies") { Content = JsonContent.Create(new { body = "Nested" }) };
        nested.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.SendAsync(nested)).StatusCode);
        (await writer.DeleteAsync($"/api/v1/community/posts/{id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await writer.GetAsync($"/api/v1/community/posts/{id}")).StatusCode);
        await using var scope = app.Services.CreateAsyncScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().CommunityPosts.AnyAsync(p => p.Id == replyId));
    }
    [Fact]
    public async Task FeedFiltersSortsAndPaginatesWithoutGrantingPrivateMaterialAccess()
    {
        var account = await app.RegisterAsync(); using var client = app.AsUser(account.Access);
        var course = await (await client.PostAsJsonAsync("/api/v1/courses", new { title = "Forum test course" })).Content.ReadFromJsonAsync<JsonElement>();
        var courseId = course.GetProperty("id").GetGuid();
        var older = await Create(client, body: new { title = "Earlier question", body = "Context", kind = "Question", courseId });
        var latest = await Create(client, body: new { title = "Later tip", body = "Context", kind = "Study tip", courseId });
        await Create(client, body: new { title = "A campus question", body = "Context", kind = "Question" });
        var olderId = older.GetProperty("id").GetGuid();
        (await client.PutAsJsonAsync($"/api/v1/community/posts/{olderId}/vote", new { value = 1 })).EnsureSuccessStatusCode();
        var top = await client.GetFromJsonAsync<JsonElement>($"/api/v1/community/posts?courseId={courseId}&sort=top&limit=1");
        Assert.Equal(2, top.GetProperty("total").GetInt32());
        Assert.Equal(olderId, top.GetProperty("data")[0].GetProperty("id").GetGuid());
        var recent = await client.GetFromJsonAsync<JsonElement>($"/api/v1/community/posts?courseId={courseId}&sort=recent&limit=1");
        Assert.Equal(latest.GetProperty("id").GetGuid(), recent.GetProperty("data")[0].GetProperty("id").GetGuid());
        var second = await client.GetFromJsonAsync<JsonElement>($"/api/v1/community/posts?courseId={courseId}&sort=recent&limit=1&page=2");
        Assert.Equal(olderId, second.GetProperty("data")[0].GetProperty("id").GetGuid());
        var filtered = await client.GetFromJsonAsync<JsonElement>($"/api/v1/community/posts?courseId={courseId}&kind=Question");
        Assert.Equal(1, filtered.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/community/posts", new { title = "Forged", body = "Context", authorId = "somebody-else", isSample = true })).StatusCode);
        var outsider = await app.RegisterAsync(); using var other = app.AsUser(outsider.Access);
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync($"/api/v1/community/posts/{olderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/courses/{courseId}")).StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedRequestsCannotReadOrWriteCommunity()
    {
        using var guest = app.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.GetAsync("/api/v1/community/posts")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await guest.PostAsJsonAsync("/api/v1/community/posts", new { title = "No", body = "No" })).StatusCode);
    }
    [Fact]
    public async Task SampleSeedIsRepeatableAndIdentitiesCannotSignIn()
    {
        await using var scope = app.Services.CreateAsyncScope();
        var seeder = ActivatorUtilities.CreateInstance<CommunitySeeder>(scope.ServiceProvider);
        var first = await seeder.SeedAsync(); var again = await seeder.SeedAsync();
        Assert.Equal(first, again); Assert.Equal(8, first.Users); Assert.Equal(10, first.Posts);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var samples = await db.Users.Where(u => u.Id.StartsWith("uni-demo-community-")).ToArrayAsync();
        Assert.Equal(8, samples.Length);
        Assert.All(samples, u => { Assert.False(u.IsActive); Assert.False(u.EmailConfirmed); Assert.Null(u.PasswordHash); });
        Assert.Equal(10, await db.CommunityPosts.CountAsync(p => p.IsSample && p.ParentId == null));
        Assert.True(first.Replies > 20 && first.Votes > 50);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.PostAsJsonAsync("/api/v1/accounts/login", new { email = samples[0].Email, password = ApiFixture.Password })).StatusCode);
        var viewer = await app.RegisterAsync(); using var client = app.AsUser(viewer.Access);
        var feed = await client.GetFromJsonAsync<JsonElement>("/api/v1/community/posts?sort=top&limit=1");
        Assert.Single(feed.GetProperty("data").EnumerateArray());
        Assert.True(feed.GetProperty("total").GetInt32() >= 10);
    }
}
