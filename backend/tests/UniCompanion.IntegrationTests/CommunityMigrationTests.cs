using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using UniCompanion.Infrastructure.Persistence;
using Xunit;

namespace UniCompanion.IntegrationTests;

public sealed class CommunityMigrationTests(ApiFixture app) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task ExistingPostHashBackfillPreservesCreationReplayWithUnicodeAndQuotes()
    {
        var owner = await app.RegisterAsync(); using var client = app.AsUser(owner.Access);
        var id = Guid.NewGuid(); var key = Guid.NewGuid().ToString("N");
        const string title = "Une question 🧠"; const string body = "Une ligne, des accents é et une \"citation\".\nDeuxième ligne.";
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var uid = await db.Users.Where(u => u.Email == owner.Email).Select(u => u.Id).SingleAsync();
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261021204947_Community");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"CommunityPosts\" (\"Id\", \"AuthorId\", \"Title\", \"Body\", \"Kind\", \"IsAnonymous\", \"IsSample\", \"RequestKey\", \"CreatedAt\") VALUES ({id}, {uid}, {title}, {body}, {"Question"}, {true}, {false}, {key}, {DateTimeOffset.UtcNow})");
            await migrator.MigrateAsync();
            Assert.True(await db.ThreadSubscriptions.AnyAsync(s => s.ThreadId == id && s.OwnerId == uid && s.Enabled));
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/community/posts") { Content = JsonContent.Create(new { title, body, kind = "Question", isAnonymous = true }) };
        request.Headers.Add("Idempotency-Key", key);
        var response = await client.SendAsync(request); response.EnsureSuccessStatusCode();
        Assert.Equal(id, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
    }
}
