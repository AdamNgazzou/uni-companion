using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniCompanion.Infrastructure.Courses;
using UniCompanion.Infrastructure.Identity;
using UniCompanion.Infrastructure.Persistence;
using Xunit;

namespace UniCompanion.IntegrationTests;

public sealed class CourseTests(ApiFixture app) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task CourseCreationRetainsOwnershipAndRecognitionIsAdminOnly()
    {
        var owner = await app.RegisterAsync();
        using var client = app.AsUser(owner.Access);
        var response = await client.PostAsJsonAsync("/api/v1/courses/", new { title = "Student course", description = "Notes" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var course = await response.Content.ReadFromJsonAsync<JsonElement>();
        var id = course.GetProperty("id").GetGuid();
        Assert.False(course.GetProperty("isCurated").GetBoolean());
        Assert.False(course.GetProperty("isRecognized").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/v1/courses/{id}/recognition", new { recognized = true })).StatusCode);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var user = (await users.FindByEmailAsync(owner.Email))!;
            Assert.True((await roles.CreateAsync(new IdentityRole("Admin"))).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, "Admin")).Succeeded);
            Assert.True((await users.UpdateSecurityStampAsync(user)).Succeeded);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/accounts/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.PostAsJsonAsync("/api/v1/accounts/refresh", new { refreshToken = owner.Refresh })).StatusCode);
        var tokens = await app.LoginAsync(owner.Email);
        using var admin = app.AsUser(tokens.Access);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/v1/courses/{id}/recognition", new { recognized = true })).StatusCode);
        var detail = await (await admin.GetAsync($"/api/v1/courses/{id}")).Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(course.GetProperty("creatorId").GetString(), detail.GetProperty("creatorId").GetString());
        Assert.True(detail.GetProperty("isRecognized").GetBoolean());
    }

    [Fact]
    public async Task CatalogueImportIsIdempotentAndDownloadsRequireMembership()
    {
        using var snapshot = new CatalogueFixture();
        Guid courseId;
        Guid materialId;
        string expectedHash;
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var importer = ActivatorUtilities.CreateInstance<CatalogueImporter>(scope.ServiceProvider);
            Assert.Equal((1, 2), await importer.ImportAsync(snapshot.Path));
            Assert.Equal((1, 2), await importer.ImportAsync(snapshot.Path));
            Assert.Equal(1, await db.Courses.CountAsync(c => c.IsCurated));
            Assert.Equal(2, await db.Materials.CountAsync());
            Assert.Equal(2, await db.MaterialVersions.CountAsync());
            var material = await db.Materials.FirstAsync(m => m.ContentType == "application/pdf");
            courseId = material.CourseId;
            materialId = material.Id;
            expectedHash = (await db.MaterialVersions.SingleAsync(v => v.Id == material.CurrentVersionId)).Sha256;
            Assert.Equal(snapshot.PdfHash, expectedHash);
        }
        var member = await app.RegisterAsync();
        var stranger = await app.RegisterAsync();
        using var joined = app.AsUser(member.Access);
        using var other = app.AsUser(stranger.Access);
        var preview = await other.GetFromJsonAsync<JsonElement>($"/api/v1/courses/{courseId}/preview");
        Assert.False(preview.GetProperty("joined").GetBoolean());
        Assert.False(preview.GetProperty("canRead").GetBoolean());
        Assert.False(preview.TryGetProperty("materials", out _));
        Assert.False(preview.TryGetProperty("structure", out _));
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.GetAsync($"/api/v1/courses/{courseId}/preview")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await joined.GetAsync($"/api/v1/materials/{materialId}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await joined.PostAsync($"/api/v1/courses/{courseId}/join", null)).StatusCode);
        var joinedPreview = await joined.GetFromJsonAsync<JsonElement>($"/api/v1/courses/{courseId}/preview");
        Assert.True(joinedPreview.GetProperty("joined").GetBoolean());
        Assert.True(joinedPreview.GetProperty("canRead").GetBoolean());
        var joinedDetail = await joined.GetFromJsonAsync<JsonElement>($"/api/v1/courses/{courseId}");
        Assert.NotEmpty(joinedDetail.GetProperty("structure").GetProperty("chapters").EnumerateArray());
        Assert.All(joinedDetail.GetProperty("structure").GetProperty("chapters").EnumerateArray(), chapter =>
            Assert.Equal(["id", "title"], chapter.EnumerateObject().Select(p => p.Name)));
        Assert.Equal(HttpStatusCode.NoContent, (await joined.PostAsync($"/api/v1/courses/{courseId}/join", null)).StatusCode);
        var file = await joined.GetAsync($"/api/v1/materials/{materialId}/content");
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal(expectedHash, Convert.ToHexStringLower(SHA256.HashData(await file.Content.ReadAsByteArrayAsync())));
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/materials/{materialId}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/v1/courses/{courseId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.GetAsync($"/api/v1/materials/{materialId}/content")).StatusCode);
    }
}
