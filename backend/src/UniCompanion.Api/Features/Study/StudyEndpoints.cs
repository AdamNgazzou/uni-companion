using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniCompanion.Core.Study;
using UniCompanion.Infrastructure.Persistence;
using UniCompanion.Infrastructure.Study;
using UniCompanion.Infrastructure.Storage;

namespace UniCompanion.Api.Features.Study;

public static class StudyEndpoints
{
    public static IEndpointRouteBuilder MapStudy(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1").WithTags("Study").RequireAuthorization();
        group.MapPost("/courses/{id:guid}/study-jobs", async (Guid id, StudyRequest request, HttpContext context, StudyJobs jobs, CancellationToken ct) =>
        {
            var job = await jobs.EnqueueAsync(id, Owner(context), Key(context), request, ct);
            return Results.Accepted($"/api/v1/study-jobs/{job.Id}", new { job.Id, job.State });
        });
        group.MapGet("/study-jobs/{id:guid}", GetAsync);
        group.MapGet("/study-artifacts/{id:guid}", GetAsync);
        group.MapGet("/study-jobs", async (Guid? courseId, string? kind, HttpContext context, AppDbContext db, StudyAccess access, StudyJobs jobs, CancellationToken ct) =>
        {
            var owner = Owner(context);
            var query = db.StudyJobs.AsNoTracking().Where(j => j.OwnerId == owner);
            if (courseId is { } course) query = query.Where(j => j.CourseId == course);
            if (kind is { } filter) query = query.Where(j => j.Kind == filter);
            var rows = await query.OrderByDescending(j => j.CreatedAt).Take(100).ToListAsync(ct);
            var labels = await jobs.DescribeAsync(rows, ct);
            var visible = new List<object>();
            foreach (var job in rows)
                if (await access.CanReadAsync(job.CourseId, owner, ct)) visible.Add(new { job.Id, job.CourseId, job.Kind, job.State, job.Stage, job.CreatedAt, labels[job.Id].Title, labels[job.Id].CourseTitle, labels[job.Id].ScopeTitle, request = JsonSerializer.Deserialize<StudyRequest>(job.RequestJson, StudyJobs.Json) });
            return Results.Ok(new { data = visible });
        });
        group.MapPost("/study-jobs/{id:guid}/cancel", async (Guid id, HttpContext context, AppDbContext db, StudyJobs jobs, TimeProvider clock, CancellationToken ct) =>
        {
            await jobs.GetAsync(id, Owner(context), ct);
            await db.StudyJobs.Where(j => j.Id == id && j.OwnerId == Owner(context) && (j.State == JobStates.Queued || j.State == JobStates.Retry || j.State == JobStates.Running))
                .ExecuteUpdateAsync(s => s.SetProperty(j => j.State, JobStates.Cancelled).SetProperty(j => j.Stage, "Cancelled")
                    .SetProperty(j => j.LeaseUntil, (DateTimeOffset?)null).SetProperty(j => j.UpdatedAt, clock.GetUtcNow()), ct);
            return Results.NoContent();
        });
        group.MapPost("/quizzes/{id:guid}/attempts", async (Guid id, AttemptRequest request, HttpContext context, StudyJobs jobs, CancellationToken ct) =>
            Results.Ok(await jobs.SubmitAsync(id, Owner(context), Key(context), request, ct)));
        group.MapGet("/quiz-attempts", async (int? page, Guid? courseId, HttpContext context, AppDbContext db, StudyJobs jobs, CancellationToken ct) =>
        {
            var owner = Owner(context);
            var number = Math.Clamp(page ?? 1, 1, 10000);
            var admin = await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id where ur.UserId == owner && role.Name == "Admin" select ur.UserId).AnyAsync(ct);
            var rows = await (from a in db.QuizAttempts.AsNoTracking()
                              join j in db.StudyJobs on a.JobId equals j.Id
                              where a.OwnerId == owner && (courseId == null || j.CourseId == courseId) && (admin || db.CourseMemberships.Any(m => m.CourseId == j.CourseId && m.UserId == owner))
                              orderby a.CreatedAt descending
                              select a).Skip((number - 1) * 20).Take(20).ToListAsync(ct);
            var jobIds = rows.Select(a => a.JobId).ToArray();
            var labels = await jobs.DescribeAsync(await db.StudyJobs.AsNoTracking().Where(j => jobIds.Contains(j.Id)).ToArrayAsync(ct), ct);
            return Results.Ok(new { data = rows.Select(a => AttemptView(a, labels[a.JobId])), page = number });
        });
        group.MapGet("/quiz-attempts/{id:guid}", async (Guid id, HttpContext context, AppDbContext db, StudyJobs jobs, CancellationToken ct) =>
        {
            var attempt = await db.QuizAttempts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id && a.OwnerId == Owner(context), ct);
            if (attempt is null) return Results.NotFound();
            var job = await jobs.GetAsync(attempt.JobId, Owner(context), ct);
            var labels = await jobs.DescribeAsync([job], ct);
            return Results.Ok(AttemptView(attempt, labels[job.Id]));
        });
        group.MapGet("/materials/{id:guid}/versions/{versionId:guid}/content", async (Guid id, Guid versionId, HttpContext context,
            AppDbContext db, StudyAccess access, PrivateFileStore files, CancellationToken ct) =>
        {
            var material = await db.Materials.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, ct);
            if (material is null || !await access.CanReadAsync(material.CourseId, Owner(context), ct)) return Results.NotFound();
            var version = await db.MaterialVersions.AsNoTracking().SingleOrDefaultAsync(v => v.Id == versionId && v.MaterialId == id, ct);
            if (version is null || !File.Exists(files.PathFor(version.Sha256))) return Results.NotFound();
            context.Response.Headers["X-Content-Type-Options"] = "nosniff";
            return Results.File(files.PathFor(version.Sha256), material.ContentType, material.FileName, enableRangeProcessing: true);
        });
        group.MapGet("/materials/{id:guid}/versions/{versionId:guid}/pages/{page:int}", async (Guid id, Guid versionId, int page,
            HttpContext context, AppDbContext db, StudyAccess access, CancellationToken ct) =>
        {
            var material = await db.Materials.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id, ct);
            if (material is null || !await access.CanReadAsync(material.CourseId, Owner(context), ct)) return Results.NotFound();
            var version = await db.MaterialVersions.AsNoTracking().SingleOrDefaultAsync(v => v.Id == versionId && v.MaterialId == id, ct);
            if (version is null || page < 1 || page > version.PageCount) return Results.NotFound();
            var text = await db.DocumentChunks.AsNoTracking().Where(c => c.MaterialVersionId == versionId && c.Page == page).OrderBy(c => c.Ordinal).Select(c => c.Text).ToArrayAsync(ct);
            return Results.Ok(new { page, text = string.Join("\n\n", text) });
        });
        return endpoints;
    }

    private static async Task<IResult> GetAsync(Guid id, HttpContext context, StudyJobs jobs, CancellationToken ct)
    {
        var job = await jobs.GetAsync(id, Owner(context), ct);
        object? result = job.State == JobStates.Succeeded && job.ResultJson is not null
            ? StudyJobs.PublicResult(JsonSerializer.Deserialize<StudyResult>(job.ResultJson, StudyJobs.Json)!) : null;
        var labels = (await jobs.DescribeAsync([job], ct))[job.Id];
        return Results.Ok(new
        {
            labels.Title,
            labels.CourseTitle,
            labels.ScopeTitle,
            job.Id,
            job.CourseId,
            job.Kind,
            job.State,
            job.Stage,
            job.ErrorCode,
            job.CreatedAt,
            job.UpdatedAt,
            request = JsonSerializer.Deserialize<StudyRequest>(job.RequestJson, StudyJobs.Json),
            stale = await jobs.IsStaleAsync(job, ct),
            result
        });
    }
    private static object AttemptView(QuizAttempt attempt, StudyLabels labels)
    {
        var result = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(attempt.ResultJson)!;
        result["title"] = JsonSerializer.SerializeToElement(labels.Title);
        result["courseTitle"] = JsonSerializer.SerializeToElement(labels.CourseTitle);
        return result;
    }
    private static string Owner(HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static string Key(HttpContext context) => context.Request.Headers["Idempotency-Key"].ToString();
}
