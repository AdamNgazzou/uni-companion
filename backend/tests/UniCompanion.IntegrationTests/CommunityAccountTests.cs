using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniCompanion.Core.Community;
using UniCompanion.Infrastructure.Identity;
using UniCompanion.Infrastructure.Persistence;
using Xunit;

namespace UniCompanion.IntegrationTests;

public sealed class CommunityAccountTests(ApiFixture app) : IClassFixture<ApiFixture>
{
    private static async Task<JsonElement> Create(HttpClient client, string path, object body, string? key = null)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        message.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString("N"));
        var result = await client.SendAsync(message); result.EnsureSuccessStatusCode();
        return await result.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task OwnAnonymousHistoryEditingAndCreationReplayStayPrivate()
    {
        var owner = await app.RegisterAsync(); var stranger = await app.RegisterAsync();
        using var writer = app.AsUser(owner.Access); using var other = app.AsUser(stranger.Access);
        var key = Guid.NewGuid().ToString("N");
        var original = new { title = "Original question", body = "Original text", isAnonymous = true, kind = "Question" };
        var created = await Create(writer, "/api/v1/community/posts", original, key); var id = created.GetProperty("id").GetGuid();
        var mine = await writer.GetFromJsonAsync<JsonElement>("/api/v1/accounts/me/discussions");
        Assert.Contains(mine.GetProperty("data").EnumerateArray(), p => p.GetProperty("post").GetProperty("id").GetGuid() == id);
        var notMine = await other.GetFromJsonAsync<JsonElement>("/api/v1/accounts/me/discussions?authorId=ignored");
        Assert.Empty(notMine.GetProperty("data").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await other.PatchAsJsonAsync($"/api/v1/community/posts/{id}", new { title = "Stolen", body = "Changed" })).StatusCode);
        (await writer.PatchAsJsonAsync($"/api/v1/community/posts/{id}", new { title = "Edited question", body = "Edited text" })).EnsureSuccessStatusCode();
        var replay = await Create(writer, "/api/v1/community/posts", original, key);
        Assert.Equal(id, replay.GetProperty("id").GetGuid()); Assert.Equal("Edited text", replay.GetProperty("body").GetString());
        var visible = await other.GetFromJsonAsync<JsonElement>($"/api/v1/community/posts/{id}");
        Assert.Equal("Anonymous student", visible.GetProperty("authorName").GetString());
        Assert.False(visible.TryGetProperty("authorId", out _)); Assert.True(visible.TryGetProperty("editedAt", out _));
        var reply = await Create(other, $"/api/v1/community/posts/{id}/replies", new { body = "Helpful explanation" });
        var rid = reply.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/api/v1/community/posts/{id}/resolution", new { replyId = rid })).StatusCode);
        (await writer.PutAsJsonAsync($"/api/v1/community/posts/{id}/resolution", new { replyId = rid })).EnsureSuccessStatusCode();
        (await other.DeleteAsync($"/api/v1/community/posts/{rid}")).EnsureSuccessStatusCode();
        var root = await writer.GetFromJsonAsync<JsonElement>($"/api/v1/community/posts/{id}");
        Assert.Equal(JsonValueKind.Null, root.GetProperty("acceptedReplyId").ValueKind);
        (await writer.DeleteAsync($"/api/v1/community/posts/{id}")).EnsureSuccessStatusCode();
        var deletedReplay = await Create(writer, "/api/v1/community/posts", original, key);
        Assert.True(deletedReplay.GetProperty("isDeleted").GetBoolean());
        Assert.DoesNotContain("Original text", deletedReplay.ToString());
    }

    [Fact]
    public async Task FollowingBookmarksAndAnonymousBlockingAreOwnedAndDoNotLinkIdentityModes()
    {
        var owner = await app.RegisterAsync(); var viewer = await app.RegisterAsync();
        using var writer = app.AsUser(owner.Access); using var reader = app.AsUser(viewer.Access);
        var anonymous = await Create(writer, "/api/v1/community/posts", new { title = "Anonymous post", body = "Context", isAnonymous = true });
        var named = await Create(writer, "/api/v1/community/posts", new { title = "Named post", body = "Context" });
        var id = anonymous.GetProperty("id").GetGuid(); var namedId = named.GetProperty("id").GetGuid();
        var following = await writer.GetFromJsonAsync<JsonElement>($"/api/v1/community/posts/{id}/subscription");
        Assert.True(following.GetProperty("following").GetBoolean());
        (await writer.PutAsJsonAsync($"/api/v1/community/posts/{id}/subscription", new { following = false })).EnsureSuccessStatusCode();
        await Task.WhenAll(reader.PutAsync($"/api/v1/bookmarks/thread/{id}", null), reader.PutAsync($"/api/v1/bookmarks/thread/{id}", null));
        var saved = await reader.GetFromJsonAsync<JsonElement>("/api/v1/bookmarks"); Assert.Single(saved.GetProperty("data").EnumerateArray());
        var othersSaved = await writer.GetFromJsonAsync<JsonElement>("/api/v1/bookmarks"); Assert.Empty(othersSaved.GetProperty("data").EnumerateArray());
        (await reader.PostAsync($"/api/v1/community/posts/{id}/block", null)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/api/v1/community/posts/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"/api/v1/community/posts/{namedId}")).StatusCode);
        var blocks = await reader.GetFromJsonAsync<JsonElement>("/api/v1/community/blocks");
        Assert.DoesNotContain("Test Student", blocks.ToString()); Assert.DoesNotContain(owner.Email, blocks.ToString());
        var blockId = blocks.GetProperty("data")[0].GetProperty("id").GetGuid();
        (await writer.DeleteAsync($"/api/v1/community/blocks/{blockId}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await reader.GetAsync($"/api/v1/community/posts/{id}")).StatusCode);
        (await reader.DeleteAsync($"/api/v1/community/blocks/{blockId}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync($"/api/v1/community/posts/{id}")).StatusCode);
    }

    [Fact]
    public async Task ReportsRequireAdministratorAndRepliesOpenAtTheirActualPage()
    {
        var owner = await app.RegisterAsync(); using var client = app.AsUser(owner.Access);
        var post = await Create(client, "/api/v1/community/posts", new { title = "Discussion", body = "Context" }); var id = post.GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync($"/api/v1/community/posts/{id}/report", new { reason = "Please review this" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/community/reports")).StatusCode);
        Guid last;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == owner.Email);
            for (var i = 0; i < 31; i++) db.CommunityPosts.Add(new CommunityPost { AuthorId = user.Id, ParentId = id, Body = "Reply " + i, RequestKey = Guid.NewGuid().ToString("N"), CreatedAt = DateTimeOffset.UtcNow.AddMinutes(i) });
            await db.SaveChangesAsync();
            last = await db.CommunityPosts.Where(p => p.ParentId == id).OrderByDescending(p => p.CreatedAt).Select(p => p.Id).FirstAsync();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync("Admin")) Assert.True((await roles.CreateAsync(new IdentityRole("Admin"))).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "Admin")).Succeeded);
            await users.UpdateSecurityStampAsync(user);
        }
        using var admin = app.AsUser((await app.LoginAsync(owner.Email)).Access);
        var mine = await admin.GetFromJsonAsync<JsonElement>("/api/v1/accounts/me/discussions?kind=replies");
        Assert.Equal(2, mine.GetProperty("data").EnumerateArray().Single(r => r.GetProperty("post").GetProperty("id").GetGuid() == last).GetProperty("replyPage").GetInt32());
        var reports = await admin.GetFromJsonAsync<JsonElement>("/api/v1/community/reports"); var rid = reports.GetProperty("data")[0].GetProperty("id").GetGuid();
        (await admin.PutAsJsonAsync($"/api/v1/community/reports/{rid}", new { action = "remove" })).EnsureSuccessStatusCode();
        var deleted = await admin.GetFromJsonAsync<JsonElement>($"/api/v1/community/posts/{id}"); Assert.True(deleted.GetProperty("isDeleted").GetBoolean());
        Assert.Equal(31, (await admin.GetFromJsonAsync<JsonElement>($"/api/v1/community/posts/{id}/replies")).GetProperty("total").GetInt32());
    }
    [Fact]
    public async Task ResourceBookmarksRecheckMembershipOnEveryRead()
    {
        var owner = await app.RegisterAsync(); var outsider = await app.RegisterAsync();
        using var client = app.AsUser(owner.Access); using var other = app.AsUser(outsider.Access);
        var course = await (await client.PostAsJsonAsync("/api/v1/courses", new { title = "Bookmark course" })).Content.ReadFromJsonAsync<JsonElement>();
        var cid = course.GetProperty("id").GetGuid(); var mid = Guid.NewGuid();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Materials.Add(new UniCompanion.Core.Courses.Material { Id = mid, CourseId = cid, Title = "Private source", FileName = "source.pdf", SourceId = "bookmark-source" });
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsync($"/api/v1/bookmarks/material/{mid}", null)).StatusCode);
        (await client.PutAsync($"/api/v1/bookmarks/material/{mid}", null)).EnsureSuccessStatusCode();
        Assert.Single((await client.GetFromJsonAsync<JsonElement>("/api/v1/bookmarks")).GetProperty("data").EnumerateArray());
        await using (var scope = app.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().CourseMemberships.Where(m => m.CourseId == cid).ExecuteDeleteAsync();
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/api/v1/bookmarks")).GetProperty("data").EnumerateArray());
    }
}
