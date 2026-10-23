using Microsoft.EntityFrameworkCore;
using UniCompanion.Core.Community;
using UniCompanion.Core.Study;
using UniCompanion.Infrastructure.Persistence;

namespace UniCompanion.Infrastructure.Activity;

public sealed class ActivityEvents(AppDbContext db, TimeProvider clock)
{
    private async Task AddAsync(string key, string kind, Guid source, string? actor, string[] recipients, CancellationToken ct)
    {
        if (recipients.Length == 0) return;
        if (db.Database.CurrentTransaction is null) throw new InvalidOperationException("Activity must commit with its source mutation.");
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"ActivityEvents\" (\"Id\", \"EventKey\", \"Kind\", \"SourceId\", \"ActorId\", \"RecipientIds\", \"CreatedAt\") VALUES ({Guid.NewGuid()}, {key}, {kind}, {source}, {actor}, {recipients}, {clock.GetUtcNow()}) ON CONFLICT (\"EventKey\") DO NOTHING", ct);
    }
    public async Task ReplyAsync(CommunityPost post, CancellationToken ct)
    {
        if (post.IsSample || post.ParentId is not { } thread) return;
        var recipients = await (from s in db.ThreadSubscriptions
                                join user in db.Users on s.OwnerId equals user.Id
                                where s.ThreadId == thread && s.Enabled && s.OwnerId != post.AuthorId && user.IsActive && user.EmailConfirmed
                                select user.Id).Distinct().ToArrayAsync(ct);
        await AddAsync("reply:" + post.Id, "reply", post.Id, post.AuthorId, recipients, ct);
    }
    public async Task UpvoteAsync(CommunityPost post, string actor, CancellationToken ct)
    {
        if (post.IsSample || post.AuthorId == actor) return;
        var recipients = await db.Users.Where(u => u.Id == post.AuthorId && u.IsActive && u.EmailConfirmed).Select(u => u.Id).ToArrayAsync(ct);
        await AddAsync("upvote:" + post.Id + ":" + actor, "upvote", post.Id, actor, recipients, ct);
    }
    public async Task StudyAsync(Guid id, CancellationToken ct)
    {
        var job = await db.StudyJobs.AsNoTracking().SingleAsync(j => j.Id == id, ct);
        if (job.State is not (JobStates.Succeeded or JobStates.Failed)) return;
        await AddAsync("study:" + id, job.State == JobStates.Succeeded ? "study_completed" : "study_failed", id, null, [job.OwnerId], ct);
    }
}
