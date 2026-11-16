using Microsoft.EntityFrameworkCore;
using UniCompanion.Core.Activity;
using UniCompanion.Infrastructure.Persistence;
using UniCompanion.Infrastructure.Study;

namespace UniCompanion.Infrastructure.Activity;

public sealed class ActivityDispatcher(AppDbContext db, StudyAccess access, TimeProvider clock)
{
    public async Task<bool> DispatchNextAsync(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.ActivityEvents.FromSqlRaw("SELECT * FROM \"ActivityEvents\" WHERE \"DispatchedAt\" IS NULL ORDER BY \"CreatedAt\", \"Id\" LIMIT 1 FOR UPDATE SKIP LOCKED").ToArrayAsync(ct);
        if (rows.Length == 0) return false;
        var activity = rows[0];
        var recipients = await db.Users.Where(u => activity.RecipientIds.Contains(u.Id) && u.IsActive && u.EmailConfirmed && (u.LockoutEnd == null || u.LockoutEnd <= clock.GetUtcNow())).Select(u => u.Id).ToArrayAsync(ct);
        var preferences = await db.ActivityPreferences.Where(p => recipients.Contains(p.OwnerId)).ToDictionaryAsync(p => p.OwnerId, ct);
        var eligible = new List<string>();
        if (activity.Kind.StartsWith("study_", StringComparison.Ordinal))
        {
            var job = await db.StudyJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == activity.SourceId, ct);
            if (job is not null && recipients.Contains(job.OwnerId) && (!preferences.TryGetValue(job.OwnerId, out var preference) || preference.StudyJobs) && await access.CanReadAsync(job.CourseId, job.OwnerId, ct)) eligible.Add(job.OwnerId);
        }
        else
        {
            var post = await db.CommunityPosts.AsNoTracking().SingleOrDefaultAsync(p => p.Id == activity.SourceId && !p.IsDeleted && !p.IsSample, ct);
            var root = post is null ? null : post.ParentId is null ? post : await db.CommunityPosts.AsNoTracking().SingleOrDefaultAsync(p => p.Id == post.ParentId && !p.IsDeleted, ct);
            if (post is not null && root is not null)
            {
                var following = await db.ThreadSubscriptions.Where(s => s.ThreadId == root.Id && s.Enabled && recipients.Contains(s.OwnerId)).Select(s => s.OwnerId).ToArrayAsync(ct);
                var blocked = await db.CommunityBlocks.Where(b => recipients.Contains(b.OwnerId) &&
                    ((b.BlockedUserId == post.AuthorId && b.IsAnonymous == post.IsAnonymous) ||
                     (b.BlockedUserId == root.AuthorId && b.IsAnonymous == root.IsAnonymous) ||
                     (activity.Kind == "upvote" && b.BlockedUserId == activity.ActorId && !b.IsAnonymous))).Select(b => b.OwnerId).Distinct().ToArrayAsync(ct);
                foreach (var recipient in recipients)
                {
                    if (recipient == activity.ActorId || !following.Contains(recipient) || blocked.Contains(recipient)) continue;
                    var preference = preferences.GetValueOrDefault(recipient) ?? new ActivityPreference();
                    if (activity.Kind == "upvote" ? preference.Upvotes : root.AuthorId == recipient ? preference.Replies : preference.FollowedThreads) eligible.Add(recipient);
                }
            }
        }
        foreach (var recipient in eligible.Distinct())
        {
            var key = activity.Kind + ":" + activity.SourceId;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "ActivityItems" ("Id", "OwnerId", "GroupKey", "Kind", "SourceId", "CreatedAt", "UpdatedAt")
                VALUES ({Guid.NewGuid()}, {recipient}, {key}, {activity.Kind}, {activity.SourceId}, {activity.CreatedAt}, {activity.CreatedAt})
                ON CONFLICT ("OwnerId", "GroupKey") DO UPDATE SET
                  "ReadAt" = CASE WHEN EXCLUDED."UpdatedAt" >= "ActivityItems"."UpdatedAt" THEN NULL ELSE "ActivityItems"."ReadAt" END,
                  "UpdatedAt" = GREATEST("ActivityItems"."UpdatedAt", EXCLUDED."UpdatedAt")
                """, ct);
        }
        activity.DispatchedAt = clock.GetUtcNow();
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }
}
