using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniCompanion.Core.Practice;
using UniCompanion.Core.Study;
using UniCompanion.Infrastructure.Persistence;
using UniCompanion.Infrastructure.Study;

namespace UniCompanion.Api.Features.Practice;

public static class PracticeEndpoints
{
    public sealed record HintRequest(int Level);
    public sealed record Submission(string? Answer);
    private static string Owner(HttpContext ctx) => ctx.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static async Task<PracticeArtifact> Artifact(StudyJobs jobs, Guid id, string owner, CancellationToken ct)
    {
        var job = await jobs.GetAsync(id, owner, ct);
        if (job.Kind != "practice" || job.State != JobStates.Succeeded || job.ResultJson is null) throw new StudyRequestException(409, "practice_not_ready");
        return JsonSerializer.Deserialize<StudyResult>(job.ResultJson, StudyJobs.Json)!.Practice ?? throw new StudyRequestException(409, "practice_not_ready");
    }
    private static async Task<PracticeTask> TaskAsync(StudyJobs jobs, Guid id, string owner, string task, CancellationToken ct) =>
        (await Artifact(jobs, id, owner, ct)).Tasks.SingleOrDefault(t => t.Id == task) ?? throw new StudyRequestException(404, "not_found");
    private static object View(PracticeTask task, PracticeProgress? state, PracticeAttempt[] attempts) => new
    {
        task.Id,
        hintLevel = state?.HintLevel ?? 0,
        hints = task.Hints.Take(state?.HintLevel ?? 0),
        hintCount = task.Hints.Length,
        solutionViewed = state?.RevealedAt is not null,
        solution = state?.RevealedAt is not null ? task.Solution : null,
        solutionCode = state?.RevealedAt is not null ? task.SolutionCode : null,
        rubric = state?.RevealedAt is not null ? task.Rubric : null,
        attempts = attempts.Select(a => new { a.Id, a.Answer, a.HintsUsed, a.SolutionViewedBefore, a.CreatedAt })
    };
    public static IEndpointRouteBuilder MapPractice(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/practice/{id:guid}").RequireAuthorization().WithTags("Practice");
        group.MapGet("/progress", async (Guid id, HttpContext ctx, StudyJobs jobs, AppDbContext db, CancellationToken ct) =>
        {
            var owner = Owner(ctx); var artifact = await Artifact(jobs, id, owner, ct);
            var state = await db.PracticeProgress.AsNoTracking().Where(p => p.JobId == id && p.OwnerId == owner).ToDictionaryAsync(p => p.TaskId, ct);
            var attempts = await db.PracticeAttempts.AsNoTracking().Where(a => a.JobId == id && a.OwnerId == owner).OrderByDescending(a => a.CreatedAt).ThenBy(a => a.Id).ToArrayAsync(ct);
            return Results.Ok(new { tasks = artifact.Tasks.Select(t => View(t, state.GetValueOrDefault(t.Id), attempts.Where(a => a.TaskId == t.Id).ToArray())) });
        });
        group.MapPut("/tasks/{task}/hint", async (Guid id, string task, HintRequest request, HttpContext ctx, StudyJobs jobs, AppDbContext db, CancellationToken ct) =>
        {
            var owner = Owner(ctx); var content = await TaskAsync(jobs, id, owner, task, ct);
            if (request.Level < 1 || request.Level > content.Hints.Length) return Results.BadRequest();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await Lock(db, owner, ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "PracticeProgress" ("JobId","OwnerId","TaskId","HintLevel") VALUES ({id},{owner},{task},{request.Level})
                ON CONFLICT ("JobId","OwnerId","TaskId") DO UPDATE SET "HintLevel"=GREATEST("PracticeProgress"."HintLevel", EXCLUDED."HintLevel")
                """, ct);
            await tx.CommitAsync(ct);
            return Results.NoContent();
        });
        group.MapPut("/tasks/{task}/solution", async (Guid id, string task, HttpContext ctx, StudyJobs jobs, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var owner = Owner(ctx); await TaskAsync(jobs, id, owner, task, ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await Lock(db, owner, ct); await Reveal(db, id, owner, task, clock.GetUtcNow(), ct); await tx.CommitAsync(ct);
            return Results.NoContent();
        });
        group.MapPost("/tasks/{task}/attempts", async (Guid id, string task, Submission request, HttpContext ctx, StudyJobs jobs, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var owner = Owner(ctx); await TaskAsync(jobs, id, owner, task, ct);
            var key = ctx.Request.Headers["Idempotency-Key"].ToString(); StudyJobs.ValidateKey(key);
            var answer = request.Answer?.Trim(); if (answer is null || answer.Length is < 1 or > 12000) return Results.BadRequest();
            var hash = StudyJobs.Hash(JsonSerializer.Serialize(new { id, task, answer }, StudyJobs.Json));
            await using var tx = await db.Database.BeginTransactionAsync(ct); await Lock(db, owner, ct);
            var existing = await db.PracticeAttempts.SingleOrDefaultAsync(a => a.OwnerId == owner && a.Key == key, ct);
            if (existing is not null) return existing.RequestHash == hash ? Results.Ok(new { existing.Id }) : Results.Conflict();
            if (await db.PracticeAttempts.CountAsync(a => a.JobId == id && a.OwnerId == owner && a.TaskId == task, ct) >= 20) return Results.Conflict(new { code = "attempt_limit" });
            var state = await db.PracticeProgress.AsNoTracking().SingleOrDefaultAsync(p => p.JobId == id && p.OwnerId == owner && p.TaskId == task, ct);
            var attempt = new PracticeAttempt
            {
                JobId = id,
                TaskId = task,
                OwnerId = owner,
                Key = key,
                RequestHash = hash,
                Answer = answer,
                HintsUsed = state?.HintLevel > 0,
                SolutionViewedBefore = state?.RevealedAt is not null,
                CreatedAt = clock.GetUtcNow()
            };
            db.PracticeAttempts.Add(attempt); await db.SaveChangesAsync(ct);
            await Reveal(db, id, owner, task, clock.GetUtcNow(), ct); await tx.CommitAsync(ct);
            return Results.Ok(new { attempt.Id });
        }).RequireRateLimiting("community");
        return endpoints;
    }
    private static Task Lock(AppDbContext db, string owner, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"practice:" + owner},0))", ct);
    private static Task Reveal(AppDbContext db, Guid id, string owner, string task, DateTimeOffset now, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO "PracticeProgress" ("JobId","OwnerId","TaskId","HintLevel","RevealedAt") VALUES ({id},{owner},{task},0,{now})
        ON CONFLICT ("JobId","OwnerId","TaskId") DO UPDATE SET "RevealedAt"=COALESCE("PracticeProgress"."RevealedAt",EXCLUDED."RevealedAt")
        """, ct);
}
