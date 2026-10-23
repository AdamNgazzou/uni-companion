using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UniCompanion.Core.Study;
using UniCompanion.Infrastructure.Persistence;

namespace UniCompanion.Infrastructure.Study;

public sealed class StudyProcessor(AppDbContext db, StudyAccess access, StudyComposer composer, IServiceScopeFactory scopes,
    TimeProvider clock, ILogger<StudyProcessor> logger)
{
    public async Task<bool> ProcessNextAsync(CancellationToken stoppingToken)
    {
        var now = clock.GetUtcNow();
        await db.StudyJobs.Where(j => (j.State == JobStates.Queued || j.State == JobStates.Retry || j.State == JobStates.Running) &&
            ((j.StartedAt != null && j.StartedAt <= now.AddMinutes(-10)) ||
             (j.Attempts >= 3 && (j.State != JobStates.Running || j.LeaseUntil <= now))))
            .ExecuteUpdateAsync(s => s.SetProperty(j => j.State, JobStates.Failed).SetProperty(j => j.ErrorCode, "job_budget_exceeded").SetProperty(j => j.UpdatedAt, now), stoppingToken);
        var owner = Guid.NewGuid();
        var until = now.AddSeconds(120);
        var claimed = await db.StudyJobs.FromSqlInterpolated($"""
            UPDATE "StudyJobs" SET "State" = 'running', "Stage" = 'Preparing your sources',
                "LeaseOwner" = {owner}, "Fence" = "Fence" + 1, "LeaseUntil" = {until},
                "Attempts" = "Attempts" + 1, "StartedAt" = COALESCE("StartedAt", {now}), "UpdatedAt" = {now}
            WHERE "Id" = (
                SELECT "Id" FROM "StudyJobs" WHERE "Attempts" < 3
                AND ((("State" = 'queued' OR "State" = 'retry_wait') AND "NextAttemptAt" <= {now})
                     OR ("State" = 'running' AND "LeaseUntil" <= {now}))
                ORDER BY "CreatedAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            ) RETURNING *
            """).AsNoTracking().ToArrayAsync(stoppingToken);
        if (claimed.Length == 0) return false;
        var job = claimed[0];
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var remaining = (job.StartedAt ?? now).AddMinutes(10) - clock.GetUtcNow();
        cancellation.CancelAfter(remaining > TimeSpan.Zero ? remaining : TimeSpan.FromMilliseconds(1));
        var heartbeat = HeartbeatAsync(job, cancellation);
        try
        {
            if (!await access.CanReadAsync(job.CourseId, job.OwnerId, cancellation.Token)) throw new StudyRequestException(403, "permission_changed");
            var result = await composer.ComposeAsync(job,
                async (stage, ct) =>
                {
                    if (!await access.CanReadAsync(job.CourseId, job.OwnerId, ct)) throw new StudyRequestException(403, "permission_changed");
                    var changed = await Owned(job).Where(j => j.ProviderCalls < 12 && j.LeaseUntil > clock.GetUtcNow())
                        .ExecuteUpdateAsync(s => s.SetProperty(j => j.ProviderCalls, j => j.ProviderCalls + 1).SetProperty(j => j.Stage, stage).SetProperty(j => j.UpdatedAt, clock.GetUtcNow()), ct);
                    if (changed != 1) throw new GenerationFailure("job_budget_or_lease_exceeded", false);
                    job.ProviderCalls++;
                },
                async (key, ct) => await db.StudyCheckpoints.Where(c => c.JobId == job.Id && c.Key == key).Select(c => c.ResultJson).SingleOrDefaultAsync(ct),
                async (key, resultJson, ct) =>
                {
                    var current = clock.GetUtcNow();
                    var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
                        WITH owned AS (SELECT "Id" FROM "StudyJobs" WHERE "Id" = {job.Id} AND "State" = 'running'
                            AND "LeaseOwner" = {job.LeaseOwner} AND "Fence" = {job.Fence} AND "LeaseUntil" > {current} FOR UPDATE)
                        INSERT INTO "StudyCheckpoints" ("JobId", "Key", "ResultJson")
                        SELECT {job.Id}, {key}, CAST({resultJson} AS jsonb) FROM owned
                        ON CONFLICT ("JobId", "Key") DO UPDATE SET "ResultJson" = EXCLUDED."ResultJson"
                        """, ct);
                    if (changed != 1) throw new GenerationFailure("lease_lost", false);
                }, cancellation.Token);
            // Lock the current permission rows until publication; revocation and publication linearize.
            await using var transaction = await db.Database.BeginTransactionAsync(cancellation.Token);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"AspNetUsers\" WHERE \"Id\" = {job.OwnerId} FOR SHARE", cancellation.Token);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"CourseMemberships\" WHERE \"CourseId\" = {job.CourseId} AND \"UserId\" = {job.OwnerId} FOR SHARE", cancellation.Token);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM \"AspNetUserRoles\" WHERE \"UserId\" = {job.OwnerId} FOR SHARE", cancellation.Token);
            if (!await access.CanReadAsync(job.CourseId, job.OwnerId, cancellation.Token)) throw new StudyRequestException(403, "permission_changed");
            var json = JsonSerializer.Serialize(result, StudyJobs.Json);
            await Owned(job).Where(j => j.LeaseUntil > clock.GetUtcNow()).ExecuteUpdateAsync(s => s
                .SetProperty(j => j.State, JobStates.Succeeded).SetProperty(j => j.Stage, "Ready to study")
                .SetProperty(j => j.ResultJson, json).SetProperty(j => j.LeaseUntil, (DateTimeOffset?)null)
                .SetProperty(j => j.ErrorCode, (string?)null).SetProperty(j => j.UpdatedAt, clock.GetUtcNow()), cancellation.Token);
            await transaction.CommitAsync(cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            var shutdown = stoppingToken.IsCancellationRequested;
            await Owned(job).ExecuteUpdateAsync(s => s.SetProperty(j => j.State, shutdown ? JobStates.Retry : JobStates.Failed)
                .SetProperty(j => j.ErrorCode, shutdown ? "worker_restarting" : "job_timeout_or_lease_lost")
                .SetProperty(j => j.NextAttemptAt, clock.GetUtcNow()).SetProperty(j => j.LeaseUntil, (DateTimeOffset?)null)
                .SetProperty(j => j.Attempts, j => shutdown ? j.Attempts - 1 : j.Attempts)
                .SetProperty(j => j.UpdatedAt, clock.GetUtcNow()), CancellationToken.None);
        }
        catch (GenerationFailure error)
        {
            var retry = error.Retryable && job.Attempts < 3 && job.ProviderCalls < 12;
            var delay = error.RetryAfter ?? (5 * (1 << job.Attempts) + Random.Shared.Next(4));
            await Owned(job).ExecuteUpdateAsync(s => s.SetProperty(j => j.State, retry ? JobStates.Retry : JobStates.Failed)
                .SetProperty(j => j.ErrorCode, error.Code).SetProperty(j => j.Stage, retry ? "We’ll try again shortly" : "Could not finish this study session")
                .SetProperty(j => j.NextAttemptAt, clock.GetUtcNow().AddSeconds(Math.Clamp(delay, 1, 3600)))
                .SetProperty(j => j.LeaseUntil, (DateTimeOffset?)null).SetProperty(j => j.UpdatedAt, clock.GetUtcNow()), CancellationToken.None);
        }
        catch (StudyRequestException error)
        {
            await Owned(job).ExecuteUpdateAsync(s => s.SetProperty(j => j.State, JobStates.Failed).SetProperty(j => j.ErrorCode, error.Code)
                .SetProperty(j => j.LeaseUntil, (DateTimeOffset?)null).SetProperty(j => j.UpdatedAt, clock.GetUtcNow()), CancellationToken.None);
        }
        catch (Exception error)
        {
            logger.LogError("Study job {JobId} failed ({ErrorType})", job.Id, error.GetType().Name);
            await Owned(job).ExecuteUpdateAsync(s => s.SetProperty(j => j.State, JobStates.Failed).SetProperty(j => j.ErrorCode, "processing_failed")
                .SetProperty(j => j.LeaseUntil, (DateTimeOffset?)null).SetProperty(j => j.UpdatedAt, clock.GetUtcNow()), CancellationToken.None);
        }
        finally
        {
            await cancellation.CancelAsync();
            try { await heartbeat; } catch (OperationCanceledException) { }
        }
        return true;
    }

    private IQueryable<StudyJob> Owned(StudyJob job) => db.StudyJobs.Where(j => j.Id == job.Id && j.State == JobStates.Running && j.LeaseOwner == job.LeaseOwner && j.Fence == job.Fence && j.LeaseUntil > clock.GetUtcNow());

    private async Task HeartbeatAsync(StudyJob job, CancellationTokenSource cancellation)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20), clock);
            while (await timer.WaitForNextTickAsync(cancellation.Token))
            {
                await using var scope = scopes.CreateAsyncScope();
                var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var permissions = scope.ServiceProvider.GetRequiredService<StudyAccess>();
                if (!await permissions.CanReadAsync(job.CourseId, job.OwnerId, cancellation.Token)) { await cancellation.CancelAsync(); return; }
                var changed = await database.StudyJobs.Where(j => j.Id == job.Id && j.State == JobStates.Running && j.LeaseOwner == job.LeaseOwner && j.Fence == job.Fence && j.LeaseUntil > clock.GetUtcNow())
                    .ExecuteUpdateAsync(s => s.SetProperty(j => j.LeaseUntil, clock.GetUtcNow().AddSeconds(120)).SetProperty(j => j.UpdatedAt, clock.GetUtcNow()), cancellation.Token);
                if (changed != 1) { await cancellation.CancelAsync(); return; }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception error)
        {
            logger.LogWarning("Study heartbeat failed ({ErrorType})", error.GetType().Name);
            await cancellation.CancelAsync();
        }
    }
}
