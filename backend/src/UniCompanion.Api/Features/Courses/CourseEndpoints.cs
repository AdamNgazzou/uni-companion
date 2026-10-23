using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniCompanion.Core.Courses;
using UniCompanion.Infrastructure.Persistence;
using UniCompanion.Infrastructure.Storage;
using UniCompanion.Infrastructure.Ai;

namespace UniCompanion.Api.Features.Courses;

public static class CourseEndpoints
{
    public static IEndpointRouteBuilder MapCourses(this IEndpointRouteBuilder endpoints)
    {
        var courses = endpoints.MapGroup("/api/v1/courses").WithTags("Courses").RequireAuthorization();
        courses.MapGet("/", async (int? page, int? limit, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            var currentPage = Math.Clamp(page ?? 1, 1, 100_000);
            var size = Math.Clamp(limit ?? 20, 1, 100);
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var total = await db.Courses.CountAsync(ct);
            var data = await db.Courses.AsNoTracking().OrderBy(c => c.Title).ThenBy(c => c.Id)
                .Skip((currentPage - 1) * size).Take(size)
                .Select(c => new
                {
                    c.Id,
                    c.Title,
                    c.Description,
                    c.CreatorId,
                    c.IsCurated,
                    c.IsRecognized,
                    joined = db.CourseMemberships.Any(m => m.CourseId == c.Id && m.UserId == userId),
                    materialCount = db.Materials.Count(m => m.CourseId == c.Id)
                }).ToListAsync(ct);
            return Results.Ok(new { data, total, page = currentPage, limit = size });
        });
        courses.MapPost("/", async (CreateCourse request, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 180 || request.Description?.Length > 5000)
                return Results.BadRequest(new { message = "Provide a title up to 180 characters and description up to 5000 characters." });
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var course = new Course { Title = request.Title.Trim(), Description = request.Description, CreatorId = userId };
            db.Courses.Add(course);
            db.CourseMemberships.Add(new CourseMembership { CourseId = course.Id, UserId = userId });
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/courses/{course.Id}", new { course.Id, course.Title, course.CreatorId, course.IsCurated, course.IsRecognized });
        });
        courses.MapPost("/{id:guid}/join", async (Guid id, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            if (!await db.Courses.AnyAsync(c => c.Id == id, ct)) return Results.NotFound();
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"CourseMemberships\" (\"CourseId\", \"UserId\", \"JoinedAt\") VALUES ({id}, {userId}, {DateTimeOffset.UtcNow}) ON CONFLICT DO NOTHING", ct);
            return Results.NoContent();
        });
        courses.MapPut("/{id:guid}/recognition", async (Guid id, Recognition request, AppDbContext db, CancellationToken ct) =>
        {
            var course = await db.Courses.FindAsync([id], ct);
            if (course is null) return Results.NotFound();
            course.IsRecognized = request.Recognized;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole("Admin"));
        courses.MapPost("/{id:guid}/search", async (Guid id, SearchRequest request, HttpContext context, AppDbContext db, CourseSearch search, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > 2000) return Results.BadRequest();
            if (!await db.Courses.AnyAsync(c => c.Id == id, ct) || !await CanReadAsync(id, context.User, db, ct)) return Results.NotFound();
            var hits = await search.SearchAsync(id, context.User.FindFirstValue(ClaimTypes.NameIdentifier)!, context.User.IsInRole("Admin"), request.Question, ct);
            return Results.Ok(new { data = hits });
        }).RequireRateLimiting("accounts");
        courses.MapGet("/{id:guid}", async (Guid id, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, ct);
            if (course is null || !await CanReadAsync(id, context.User, db, ct)) return Results.NotFound();
            var materials = await db.Materials.AsNoTracking().Where(m => m.CourseId == id).OrderBy(m => m.SortOrder)
                .Select(m => new { m.Id, m.Title, m.FileName, m.ContentType, m.LessonId, m.CurrentVersionId }).ToListAsync(ct);
            using var source = JsonDocument.Parse(course.SourceMetadataJson ?? "{}");
            return Results.Ok(new
            {
                course.Id,
                course.Title,
                course.Description,
                course.CreatorId,
                course.IsCurated,
                course.IsRecognized,
                structure = source.RootElement.Clone(),
                materials
            });
        });
        endpoints.MapGet("/api/v1/materials/{id:guid}/content", async (Guid id, HttpContext context, AppDbContext db, PrivateFileStore files, CancellationToken ct) =>
        {
            var material = await db.Materials.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, ct);
            if (material is null || !await CanReadAsync(material.CourseId, context.User, db, ct)) return Results.NotFound();
            var version = await db.MaterialVersions.AsNoTracking().SingleOrDefaultAsync(v => v.Id == material.CurrentVersionId, ct);
            if (version is null || !File.Exists(files.PathFor(version.Sha256))) return Results.NotFound();
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(files.PathFor(version.Sha256), material.ContentType, material.FileName, enableRangeProcessing: true);
        }).WithTags("Materials").RequireAuthorization();
        return endpoints;
    }

    private static Task<bool> CanReadAsync(Guid courseId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct) =>
        user.IsInRole("Admin") ? Task.FromResult(true) :
        db.CourseMemberships.AnyAsync(m => m.CourseId == courseId && m.UserId == user.FindFirstValue(ClaimTypes.NameIdentifier), ct);

    public sealed record CreateCourse(string Title, string? Description);
    public sealed record SearchRequest(string Question);
    public sealed record Recognition(bool Recognized);
}
