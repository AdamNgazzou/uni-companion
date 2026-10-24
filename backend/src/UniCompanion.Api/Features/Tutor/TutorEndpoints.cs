using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniCompanion.Core.Study;
using UniCompanion.Core.Tutor;
using UniCompanion.Infrastructure.Persistence;
using UniCompanion.Infrastructure.Study;
using UniCompanion.Infrastructure.Tutor;

namespace UniCompanion.Api.Features.Tutor;

public static class TutorEndpoints
{
    private static string Owner(HttpContext ctx) => ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static string Key(HttpContext ctx) => ctx.Request.Headers["Idempotency-Key"].ToString();
    public static IEndpointRouteBuilder MapTutor(this IEndpointRouteBuilder endpoints)
    {
        var routes = endpoints.MapGroup("/api/v1").RequireAuthorization().WithTags("Tutor");
        routes.MapPost("/courses/{id:guid}/tutor-conversations", async (Guid id, ConversationRequest request, HttpContext ctx, TutorConversations tutor, CancellationToken ct) =>
        {
            var conversation = await tutor.CreateAsync(id, Owner(ctx), Key(ctx), request, ct); return Results.Ok(new { conversation.Id });
        }).RequireRateLimiting("community");
        routes.MapGet("/tutor/conversations", async (Guid? courseId, int? page, HttpContext ctx, AppDbContext db, CancellationToken ct) =>
        {
            var owner = Owner(ctx); var admin = await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id where ur.UserId == owner && role.Name == "Admin" select ur.UserId).AnyAsync(ct); var number = Math.Clamp(page ?? 1, 1, 10000);
            var query = db.TutorConversations.AsNoTracking().Where(c => c.OwnerId == owner && (courseId == null || c.CourseId == courseId) && (admin || db.CourseMemberships.Any(m => m.CourseId == c.CourseId && m.UserId == owner)));
            var total = await query.CountAsync(ct);
            var rows = await (from conversation in query join course in db.Courses on conversation.CourseId equals course.Id orderby conversation.UpdatedAt descending, conversation.Id select new { conversation.Id, conversation.Title, conversation.CourseId, courseTitle = course.Title, conversation.Language, conversation.UpdatedAt, isFeedback = conversation.PracticeAttemptId != null }).Skip((number - 1) * 20).Take(20).ToArrayAsync(ct);
            return Results.Ok(new { data = rows, total, page = number, limit = 20 });
        });
        routes.MapGet("/tutor/conversations/{id:guid}", async (Guid id, HttpContext ctx, TutorConversations tutor, AppDbContext db, StudyJobs jobs, CancellationToken ct) =>
        {
            var owner = Owner(ctx); var conversation = await tutor.GetAsync(id, owner, ct);
            var courseTitle = await db.Courses.Where(c => c.Id == conversation.CourseId).Select(c => c.Title).SingleAsync(ct);
            var scopeTitle = conversation.MaterialId is { } material ? await db.Materials.Where(m => m.Id == material && m.CourseId == conversation.CourseId).Select(m => m.Title).SingleOrDefaultAsync(ct) ?? "Earlier material" : conversation.ChapterId is not null ? "Chapter materials" : "Course PDFs";
            var rows = await (from turn in db.TutorTurns.AsNoTracking() join job in db.StudyJobs.AsNoTracking() on turn.JobId equals job.Id where turn.ConversationId == id && turn.OwnerId == owner orderby turn.Ordinal select new { turn.Id, turn.Ordinal, turn.Question, turn.Action, turn.CreatedAt, jobId = job.Id, job.State, job.Stage, job.ErrorCode, job.ResultJson }).ToArrayAsync(ct);
            var attempt = conversation.PracticeAttemptId is { } attemptId ? await db.PracticeAttempts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == attemptId && a.OwnerId == owner, ct) : null;
            var practice = attempt is null ? null : new { jobId = attempt.JobId, taskId = attempt.TaskId, attemptId = attempt.Id, attempt.Answer, usesEarlierSources = await jobs.IsStaleAsync(await jobs.GetAsync(attempt.JobId, owner, ct), ct) };
            var turns = rows.Select(row => new { row.Id, row.Ordinal, row.Question, row.Action, row.CreatedAt, row.jobId, row.State, row.Stage, row.ErrorCode, result = row.State == JobStates.Succeeded && row.ResultJson is not null ? StudyJobs.PublicResult(JsonSerializer.Deserialize<StudyResult>(row.ResultJson, StudyJobs.Json)!) : null });
            return Results.Ok(new { conversation.Id, conversation.Title, conversation.CourseId, courseTitle, scopeTitle, conversation.Language, isFeedback = conversation.PracticeAttemptId != null, practice, turns, turnLimit = 40 });
        });
        routes.MapPost("/tutor/conversations/{id:guid}/turns", async (Guid id, TurnRequest request, HttpContext ctx, TutorConversations tutor, CancellationToken ct) =>
        {
            var turn = await tutor.SendAsync(id, Owner(ctx), Key(ctx), request, ct); return Results.Accepted($"/api/v1/study-jobs/{turn.JobId}", new { turn.Id, turn.JobId });
        });
        routes.MapPost("/practice/{jobId:guid}/attempts/{attemptId:guid}/feedback", async (Guid jobId, Guid attemptId, HttpContext ctx, TutorConversations tutor, CancellationToken ct) =>
        {
            var conversation = await tutor.StartFeedbackAsync(jobId, attemptId, Owner(ctx), Key(ctx), ct); return Results.Ok(new { conversation.Id });
        });
        routes.MapDelete("/tutor/conversations/{id:guid}", async (Guid id, HttpContext ctx, TutorConversations tutor, CancellationToken ct) => { await tutor.DeleteAsync(id, Owner(ctx), ct); return Results.NoContent(); });
        routes.MapDelete("/tutor/conversations", async (HttpContext ctx, TutorConversations tutor, CancellationToken ct) => { await tutor.DeleteAsync(null, Owner(ctx), ct); return Results.NoContent(); });
        return endpoints;
    }
}
