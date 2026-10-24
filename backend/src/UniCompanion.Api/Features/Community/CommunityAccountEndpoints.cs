using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using UniCompanion.Core.Community;
using UniCompanion.Infrastructure.Persistence;
using UniCompanion.Infrastructure.Study;

namespace UniCompanion.Api.Features.Community;

public static partial class CommunityEndpoints
{
    public sealed record EditPost(string? Title, string? Body);
    public sealed record FollowThread(bool Following);
    public sealed record ResolveQuestion(Guid? ReplyId);
    public sealed record ReportPost(string? Reason);
    public sealed record ReviewReport(string Action);
    private static string Owner(HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private static void MapCommunityAccount(IEndpointRouteBuilder endpoints)
    {
        var routes = endpoints.MapGroup("/api/v1").WithTags("Community account").RequireAuthorization();
        routes.MapGet("/community/posts/{id:guid}/entry", async (Guid id, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            var post = await Views(Visible(db, Owner(context)).AsNoTracking().Where(p => p.Id == id), db, Owner(context)).SingleOrDefaultAsync(ct);
            return post is null ? Results.NotFound() : Results.Ok(post);
        });
        routes.MapGet("/accounts/me/discussions", async (string? kind, string? q, int? page, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            var term = q?.Trim().ToLowerInvariant() ?? "";
            if (kind is not (null or "threads" or "replies") || term.Length > 100) return Results.BadRequest();
            var owner = Owner(context);
            var visible = Visible(db, owner);
            var query = visible.AsNoTracking().Where(p => p.AuthorId == owner && !p.IsDeleted && (p.ParentId == null || visible.Any(root => root.Id == p.ParentId)));
            query = kind == "replies" ? query.Where(p => p.ParentId != null) : query.Where(p => p.ParentId == null);
            if (term.Length > 0) query = query.Where(p => p.Title.ToLower().Contains(term) || p.Body.ToLower().Contains(term) || visible.Any(root => root.Id == p.ParentId && root.Title.ToLower().Contains(term)));
            var number = Math.Clamp(page ?? 1, 1, 10000);
            var total = await query.CountAsync(ct);
            var posts = await Views(query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id).Skip((number - 1) * 20).Take(20), db, owner).ToArrayAsync(ct);
            var data = new List<object>();
            foreach (var post in posts)
            {
                var rootId = post.ParentId ?? post.Id;
                var root = await Visible(db, owner).AsNoTracking().Where(p => p.Id == rootId).Select(p => new { p.Title }).SingleOrDefaultAsync(ct);
                if (root is null) continue;
                data.Add(new { post, threadId = rootId, threadTitle = root.Title, replyPage = post.ParentId is null ? 1 : await ReplyPageAsync(db, owner, rootId, post.Id, ct) });
            }
            return Results.Ok(new { data, total, page = number, limit = 20 });
        });
        routes.MapGet("/community/posts/{id:guid}/subscription", async (Guid id, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            var owner = Owner(context);
            if (!await Visible(db, owner).AnyAsync(p => p.Id == id && p.ParentId == null, ct)) return Results.NotFound();
            return Results.Ok(new { following = await db.ThreadSubscriptions.AnyAsync(s => s.ThreadId == id && s.OwnerId == owner && s.Enabled, ct) });
        });
        routes.MapPut("/community/posts/{id:guid}/subscription", async (Guid id, FollowThread request, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            var owner = Owner(context);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await LockThreadAsync(db, id, ct);
            if (!await Visible(db, owner).AnyAsync(p => p.Id == id && p.ParentId == null && !p.IsDeleted, ct)) return Results.NotFound();
            var subscription = await db.ThreadSubscriptions.FindAsync([id, owner], ct);
            if (subscription is null) db.ThreadSubscriptions.Add(new ThreadSubscription { ThreadId = id, OwnerId = owner, Enabled = request.Following });
            else subscription.Enabled = request.Following;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Results.Ok(new { following = request.Following });
        }).RequireRateLimiting("community");
        routes.MapPatch("/community/posts/{id:guid}", async (Guid id, EditPost request, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            var owner = Owner(context);
            var original = await db.CommunityPosts.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && p.AuthorId == owner && !p.IsDeleted, ct);
            if (original is null) return Results.NotFound();
            var title = request.Title?.Trim() ?? "";
            var body = request.Body?.Trim() ?? "";
            if (body.Length is < 1 or > 8000 || (original.ParentId is null && title.Length is < 1 or > 180)) return Results.BadRequest();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await LockThreadAsync(db, original.ParentId ?? id, ct);
            var changed = await db.CommunityPosts.Where(p => p.Id == id && p.AuthorId == owner && !p.IsDeleted).ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Title, original.ParentId == null ? title : "")
                .SetProperty(p => p.Body, body).SetProperty(p => p.EditedAt, DateTimeOffset.UtcNow).SetProperty(p => p.AcceptedReplyId, (Guid?)null), ct);
            if (changed == 0) return Results.NotFound();
            await db.CommunityPosts.Where(p => p.AcceptedReplyId == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.AcceptedReplyId, (Guid?)null), ct);
            await tx.CommitAsync(ct);
            return Results.Ok(await Views(db.CommunityPosts.Where(p => p.Id == id), db, owner).SingleAsync(ct));
        }).RequireRateLimiting("community");
        routes.MapPut("/community/posts/{id:guid}/resolution", async (Guid id, ResolveQuestion request, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            var owner = Owner(context);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await LockThreadAsync(db, id, ct);
            var root = await db.CommunityPosts.SingleOrDefaultAsync(p => p.Id == id && p.ParentId == null && p.AuthorId == owner && p.Kind == "Question" && !p.IsDeleted, ct);
            if (root is null) return Results.NotFound();
            if (request.ReplyId is { } reply && !await Visible(db, owner).AnyAsync(p => p.Id == reply && p.ParentId == id && !p.IsDeleted, ct)) return Results.BadRequest();
            root.AcceptedReplyId = request.ReplyId;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Results.NoContent();
        }).RequireRateLimiting("community");
        routes.MapGet("/bookmarks", async (int? page, HttpContext context, AppDbContext db, StudyAccess access, CancellationToken ct) =>
        {
            var owner = Owner(context); var number = Math.Clamp(page ?? 1, 1, 10000);
            var rows = await db.SavedItems.AsNoTracking().Where(s => s.OwnerId == owner).OrderByDescending(s => s.CreatedAt).ThenBy(s => s.Id).Skip((number - 1) * 20).Take(20).ToArrayAsync(ct);
            var data = new List<object>();
            foreach (var row in rows)
            {
                if (row.Kind == "thread")
                {
                    var post = await Visible(db, owner).AsNoTracking().SingleOrDefaultAsync(p => p.Id == row.TargetId && !p.IsDeleted, ct);
                    if (post is not null) data.Add(new { row.Id, row.Kind, row.TargetId, post.Title, post.CourseId, row.CreatedAt });
                }
                else
                {
                    var material = await db.Materials.AsNoTracking().SingleOrDefaultAsync(m => m.Id == row.TargetId, ct);
                    if (material is not null && await access.CanReadAsync(material.CourseId, owner, ct))
                        data.Add(new { row.Id, row.Kind, row.TargetId, material.Title, material.CourseId, material.CurrentVersionId, material.ContentType, row.CreatedAt });
                }
            }
            return Results.Ok(new { data, page = number, hasMore = rows.Length == 20 });
        });
        routes.MapPut("/bookmarks/{kind}/{id:guid}", async (string kind, Guid id, HttpContext context, AppDbContext db, StudyAccess access, CancellationToken ct) =>
        {
            var owner = Owner(context);
            if (kind is not ("thread" or "material")) return Results.BadRequest();
            if (kind == "thread" && !await Visible(db, owner).AnyAsync(p => p.Id == id && p.ParentId == null && !p.IsDeleted, ct)) return Results.NotFound();
            if (kind == "material")
            {
                var course = await db.Materials.Where(m => m.Id == id).Select(m => (Guid?)m.CourseId).SingleOrDefaultAsync(ct);
                if (course is null || !await access.CanReadAsync(course.Value, owner, ct)) return Results.NotFound();
            }
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"SavedItems\" (\"Id\", \"OwnerId\", \"Kind\", \"TargetId\", \"CreatedAt\") VALUES ({Guid.NewGuid()}, {owner}, {kind}, {id}, {DateTimeOffset.UtcNow}) ON CONFLICT (\"OwnerId\", \"Kind\", \"TargetId\") DO NOTHING", ct);
            return Results.NoContent();
        }).RequireRateLimiting("community");
        routes.MapGet("/bookmarks/{kind}/{id:guid}", async (string kind, Guid id, HttpContext context, AppDbContext db, CancellationToken ct) =>
            Results.Ok(new { saved = await db.SavedItems.AnyAsync(s => s.OwnerId == Owner(context) && s.Kind == kind && s.TargetId == id, ct) }));
        routes.MapDelete("/bookmarks/{kind}/{id:guid}", async (string kind, Guid id, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            await db.SavedItems.Where(s => s.OwnerId == Owner(context) && s.Kind == kind && s.TargetId == id).ExecuteDeleteAsync(ct);
            return Results.NoContent();
        });
        routes.MapPost("/community/posts/{id:guid}/report", async (Guid id, ReportPost request, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            var reason = request.Reason?.Trim() ?? ""; var owner = Owner(context);
            if (reason.Length is < 1 or > 1000) return Results.BadRequest();
            if (!await Visible(db, owner).AnyAsync(p => p.Id == id && !p.IsDeleted, ct)) return Results.NotFound();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"CommunityReports\" (\"Id\", \"PostId\", \"ReporterId\", \"Reason\", \"State\", \"CreatedAt\") VALUES ({Guid.NewGuid()}, {id}, {owner}, {reason}, {"open"}, {DateTimeOffset.UtcNow}) ON CONFLICT (\"PostId\", \"ReporterId\") DO NOTHING", ct);
            return Results.Accepted();
        }).RequireRateLimiting("community");
        routes.MapPost("/community/posts/{id:guid}/block", async (Guid id, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            var owner = Owner(context);
            var post = await db.CommunityPosts.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id && !p.IsDeleted, ct);
            if (post is null || post.AuthorId == owner) return Results.NotFound();
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"CommunityBlocks\" (\"Id\", \"OwnerId\", \"BlockedUserId\", \"IsAnonymous\") VALUES ({Guid.NewGuid()}, {owner}, {post.AuthorId}, {post.IsAnonymous}) ON CONFLICT (\"OwnerId\", \"BlockedUserId\", \"IsAnonymous\") DO NOTHING", ct);
            return Results.NoContent();
        }).RequireRateLimiting("community");
        routes.MapGet("/community/blocks", async (int? page, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            var number = Math.Clamp(page ?? 1, 1, 10000);
            var data = await db.CommunityBlocks.Where(b => b.OwnerId == Owner(context)).OrderBy(b => b.Id).Skip((number - 1) * 20).Take(20).Select(b => new { b.Id, label = b.IsAnonymous ? "Blocked anonymous participant" : "Blocked profile" }).ToArrayAsync(ct);
            return Results.Ok(new { data, page = number, hasMore = data.Length == 20 });
        });
        routes.MapDelete("/community/blocks/{id:guid}", async (Guid id, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            await db.CommunityBlocks.Where(b => b.Id == id && b.OwnerId == Owner(context)).ExecuteDeleteAsync(ct); return Results.NoContent();
        });
        routes.MapGet("/community/reports", async (int? page, AppDbContext db, CancellationToken ct) =>
        {
            var number = Math.Clamp(page ?? 1, 1, 10000);
            var data = await (from report in db.CommunityReports.AsNoTracking()
                              join post in db.CommunityPosts on report.PostId equals post.Id
                              where report.State == "open"
                              orderby report.CreatedAt, report.Id
                              select new { report.Id, report.PostId, report.Reason, report.CreatedAt, post.Title, post.Body, threadId = post.ParentId ?? post.Id }).Skip((number - 1) * 20).Take(20).ToArrayAsync(ct);
            return Results.Ok(new { data, page = number });
        }).RequireAuthorization(p => p.RequireRole("Admin"));
        routes.MapPut("/community/reports/{id:guid}", async (Guid id, ReviewReport request, AppDbContext db, CancellationToken ct) =>
        {
            if (request.Action is not ("dismiss" or "remove")) return Results.BadRequest();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var report = await db.CommunityReports.SingleOrDefaultAsync(r => r.Id == id, ct);
            if (report is null) return Results.NotFound();
            var post = await db.CommunityPosts.AsNoTracking().SingleAsync(p => p.Id == report.PostId, ct);
            await LockThreadAsync(db, post.ParentId ?? post.Id, ct);
            if (request.Action == "remove") await RedactAsync(db, report.PostId, ct);
            report.State = request.Action == "remove" ? "removed" : "dismissed";
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Results.NoContent();
        }).RequireAuthorization(p => p.RequireRole("Admin"));
    }

    internal static async Task<int> ReplyPageAsync(AppDbContext db, string owner, Guid threadId, Guid replyId, CancellationToken ct)
    {
        var reply = await Visible(db, owner).AsNoTracking().SingleOrDefaultAsync(p => p.Id == replyId && p.ParentId == threadId, ct);
        if (reply is null) return 1;
        var earlier = await Visible(db, owner).CountAsync(p => p.ParentId == threadId && (p.CreatedAt < reply.CreatedAt || (p.CreatedAt == reply.CreatedAt && p.Id.CompareTo(reply.Id) < 0)), ct);
        return earlier / 30 + 1;
    }

    internal static async Task RedactAsync(AppDbContext db, Guid id, CancellationToken ct)
    {
        await db.CommunityPosts.Where(p => p.Id == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsDeleted, true).SetProperty(p => p.Title, "Deleted discussion").SetProperty(p => p.Body, "This contribution was deleted.").SetProperty(p => p.AcceptedReplyId, (Guid?)null), ct);
        await db.CommunityPosts.Where(p => p.AcceptedReplyId == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.AcceptedReplyId, (Guid?)null), ct);
    }
}
