using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using UniCompanion.Core.Courses;
using UniCompanion.Core.Study;
using UniCompanion.Infrastructure.Ai;
using UniCompanion.Infrastructure.Persistence;
using Xunit;

namespace UniCompanion.IntegrationTests;

public sealed class RetrievalTests(ApiFixture app) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task RetrievalFiltersCourseMembershipAndOldVersionsBeforeRanking()
    {
        var member = await app.RegisterAsync();
        var stranger = await app.RegisterAsync();
        using var client = app.AsUser(member.Access);
        var created = await (await client.PostAsJsonAsync("/api/v1/courses/", new { title = "Retrieval test", description = "" })).Content.ReadFromJsonAsync<JsonElement>();
        var courseId = created.GetProperty("id").GetGuid();
        Guid currentChunk;
        Guid versionId;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var material = new Material { CourseId = courseId, Title = "ML", FileName = "ML.pdf", SourceId = "retrieval-test" };
            db.Materials.Add(material);
            await db.SaveChangesAsync();
            var old = new MaterialVersion { MaterialId = material.Id, Sha256 = new string('a', 64), Bytes = 10 };
            var current = new MaterialVersion { MaterialId = material.Id, Sha256 = new string('b', 64), Bytes = 12 };
            db.MaterialVersions.AddRange(old, current);
            await db.SaveChangesAsync();
            material.CurrentVersionId = current.Id;
            versionId = current.Id;
            var vector = new float[384]; vector[0] = 1;
            var active = new DocumentChunk { MaterialVersionId = current.Id, Ordinal = 0, Page = 7, Text = "Supervised learning uses labelled examples.", Model = PythonAiBridge.Model, Embedding = vector };
            currentChunk = active.Id;
            db.DocumentChunks.AddRange(active, new DocumentChunk { MaterialVersionId = old.Id, Ordinal = 0, Page = 1, Text = "OLD SECRET supervised learning", Model = PythonAiBridge.Model, Embedding = vector });
            await db.SaveChangesAsync();
            var strangerId = (await db.Users.SingleAsync(u => u.Email == stranger.Email)).Id;
            var direct = await scope.ServiceProvider.GetRequiredService<CourseSearch>().SearchAsync(courseId, strangerId, false, "supervised learning", CancellationToken.None);
            Assert.Empty(direct);
        }
        var response = await client.PostAsJsonAsync($"/api/v1/courses/{courseId}/search", new { question = "supervised learning" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.Equal(1, data.GetArrayLength());
        Assert.Equal(currentChunk, data[0].GetProperty("chunkId").GetGuid());
        Assert.Equal(versionId, data[0].GetProperty("versionId").GetGuid());
        Assert.Equal(7, data[0].GetProperty("page").GetInt32());
        using var denied = app.AsUser(stranger.Access);
        Assert.Equal(HttpStatusCode.NotFound, (await denied.PostAsJsonAsync($"/api/v1/courses/{courseId}/search", new { question = "supervised learning" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.PostAsJsonAsync($"/api/v1/courses/{courseId}/search", new { question = "supervised learning" })).StatusCode);
    }
}

public sealed class FakeEmbedding : IQueryEmbedding
{
    public int FailuresRemaining { get; set; }
    public Task<float[]> EmbedAsync(string question, CancellationToken cancellationToken)
    {
        if (FailuresRemaining > 0) { FailuresRemaining--; throw new AiUnavailableException(); }
        var vector = new float[384]; vector[0] = 1;
        return Task.FromResult(vector);
    }
}
