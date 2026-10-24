using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using UniCompanion.Api.Features.Community;
using UniCompanion.Core.Activity;
using UniCompanion.Infrastructure.Persistence;
using UniCompanion.Infrastructure.Study;

namespace UniCompanion.Api.Features.Activity;

public static class ActivityEndpoints
{
    public sealed record Preferences(bool Replies = true, bool FollowedThreads = true, bool StudyJobs = true, bool Upvotes = false);
    private static string Owner(HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private static IQueryable<ActivityItem> Visible(AppDbContext db, string owner, bool admin) => db.ActivityItems.AsNoTracking().Where(item => item.OwnerId == owner &&
        ((item.Kind == "revision_reminder") || ((item.Kind == "study_completed" || item.Kind == "study_failed") && db.StudyJobs.Any(j => j.Id == item.SourceId && j.OwnerId == owner && (admin || db.CourseMemberships.Any(m => m.CourseId == j.CourseId && m.UserId == owner)))) ||
        ((item.Kind == "reply" || item.Kind == "upvote") && db.CommunityPosts.Any(p => p.Id == item.SourceId && !p.IsDeleted && !p.IsSample &&
            !db.CommunityBlocks.Any(b => b.OwnerId == owner && b.BlockedUserId == p.AuthorId && b.IsAnonymous == p.IsAnonymous) &&
            (p.ParentId == null || db.CommunityPosts.Any(root => root.Id == p.ParentId && !root.IsDeleted && !db.CommunityBlocks.Any(b => b.OwnerId == owner && b.BlockedUserId == root.AuthorId && b.IsAnonymous == root.IsAnonymous)))))));

    public static IEndpointRouteBuilder MapActivity(this IEndpointRouteBuilder endpoints)
    {
        var routes = endpoints.MapGroup("/api/v1/activity").WithTags("Activity").RequireAuthorization();
        routes.MapGet("/count", async (HttpContext context, AppDbContext db, CancellationToken ct) =>
            Results.Ok(new { count = await Visible(db, Owner(context), context.User.IsInRole("Admin")).CountAsync(item => item.ReadAt == null, ct) }));
        routes.MapGet("/", async (bool? unread, int? page, HttpContext context, AppDbContext db, StudyJobs jobs, CancellationToken ct) =>
        {
            var owner = Owner(context); var query = Visible(db, owner, context.User.IsInRole("Admin"));
            var unreadCount = await query.CountAsync(item => item.ReadAt == null, ct);
            if (unread == true) query = query.Where(item => item.ReadAt == null);
            var total = await query.CountAsync(ct); var number = Math.Clamp(page ?? 1, 1, 10000);
            var rows = await query.OrderByDescending(item => item.UpdatedAt).ThenBy(item => item.Id).Skip((number - 1) * 20).Take(20).ToArrayAsync(ct);
            var studyIds = rows.Where(item => item.Kind.StartsWith("study_", StringComparison.Ordinal)).Select(item => item.SourceId).ToArray();
            var studyLabels = await jobs.DescribeAsync(await db.StudyJobs.AsNoTracking().Where(j => studyIds.Contains(j.Id)).ToArrayAsync(ct), ct);
            var sourceIds = rows.Where(item => item.Kind is "reply" or "upvote").Select(item => item.SourceId).ToArray();
            var posts = await db.CommunityPosts.AsNoTracking().Where(p => sourceIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
            var roots = posts.Values.Where(p => p.ParentId != null).Select(p => p.ParentId!.Value).ToArray();
            var titles = await db.CommunityPosts.AsNoTracking().Where(p => roots.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Title, ct);
            var authorIds = posts.Values.Where(p => !p.IsAnonymous).Select(p => p.AuthorId).ToArray();
            var names = await db.Users.Where(u => authorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
            var data = new List<object>();
            foreach (var item in rows)
            {
                string title, detail;
                if (item.Kind == "revision_reminder")
                {
                    title = "A little time for revision?"; detail = "Review a missed question or a concept you saved";
                }
                else if (item.Kind.StartsWith("study_", StringComparison.Ordinal))
                {
                    if (!studyLabels.TryGetValue(item.SourceId, out var label)) continue;
                    title = item.Kind == "study_completed" ? "Your study session is ready" : "Your study session needs another try";
                    detail = label.CourseTitle + " · " + label.Title;
                }
                else if (posts.TryGetValue(item.SourceId, out var post))
                {
                    title = post.ParentId is { } root ? titles.GetValueOrDefault(root) ?? "Discussion" : post.Title;
                    detail = item.Kind == "upvote" ? "Your contribution received upvotes" :
                        (post.IsAnonymous ? "Anonymous student" : names.GetValueOrDefault(post.AuthorId) ?? "A student") + " replied in this discussion";
                }
                else continue;
                data.Add(new { item.Id, item.Kind, title, detail, item.CreatedAt, item.UpdatedAt, item.ReadAt });
            }
            return Results.Ok(new { data, total, unreadCount, page = number, limit = 20 });
        });
        routes.MapGet("/{id:guid}/destination", async (Guid id, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            var owner = Owner(context);
            var item = await Visible(db, owner, context.User.IsInRole("Admin")).SingleOrDefaultAsync(item => item.Id == id, ct);
            if (item is null) return Results.NotFound();
            if (item.Kind == "revision_reminder") return Results.Ok(new { kind = "revision", id = item.SourceId });
            if (item.Kind.StartsWith("study_", StringComparison.Ordinal)) return Results.Ok(new { kind = "study", id = item.SourceId });
            var post = await db.CommunityPosts.AsNoTracking().SingleAsync(p => p.Id == item.SourceId, ct);
            var threadId = post.ParentId ?? post.Id;
            return Results.Ok(new { kind = "thread", id = threadId, replyId = post.ParentId is not null ? (Guid?)post.Id : null, replyPage = post.ParentId is not null ? await CommunityEndpoints.ReplyPageAsync(db, owner, threadId, post.Id, ct) : 1 });
        });
        routes.MapPut("/{id:guid}/read", async (Guid id, HttpContext context, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var owner = Owner(context);
            await db.ActivityItems.Where(item => item.Id == id && item.OwnerId == owner && item.ReadAt == null).ExecuteUpdateAsync(s => s.SetProperty(item => item.ReadAt, clock.GetUtcNow()), ct);
            return Results.NoContent();
        });
        routes.MapPut("/read-all", async (HttpContext context, AppDbContext db, TimeProvider clock, CancellationToken ct) =>
        {
            var owner = Owner(context);
            await db.ActivityItems.Where(item => item.OwnerId == owner && item.ReadAt == null).ExecuteUpdateAsync(s => s.SetProperty(item => item.ReadAt, clock.GetUtcNow()), ct);
            return Results.NoContent();
        });
        routes.MapGet("/preferences", async (HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            var preference = await db.ActivityPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.OwnerId == Owner(context), ct);
            return Results.Ok(preference is null ? new Preferences() : new Preferences(preference.Replies, preference.FollowedThreads, preference.StudyJobs, preference.Upvotes));
        });
        routes.MapPut("/preferences", async (Preferences request, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            var owner = Owner(context);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "ActivityPreferences" ("OwnerId", "Replies", "FollowedThreads", "StudyJobs", "Upvotes")
                VALUES ({owner}, {request.Replies}, {request.FollowedThreads}, {request.StudyJobs}, {request.Upvotes})
                ON CONFLICT ("OwnerId") DO UPDATE SET "Replies"=EXCLUDED."Replies", "FollowedThreads"=EXCLUDED."FollowedThreads", "StudyJobs"=EXCLUDED."StudyJobs", "Upvotes"=EXCLUDED."Upvotes"
                """, ct);
            return Results.NoContent();
        }).RequireRateLimiting("community");
        return endpoints;
    }
}
