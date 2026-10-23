using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using UniCompanion.Core.Community;
using UniCompanion.Infrastructure.Persistence;

namespace UniCompanion.Api.Features.Community;

public static class CommunityEndpoints
{
    public sealed record CreatePost(string? Title, string? Body, string Kind = "Discussion", bool IsAnonymous = false, Guid? CourseId = null);
    public sealed record CreateReply(string? Body, bool IsAnonymous = false);
    public sealed record SetVote(int Value);
    public sealed record PostView(Guid Id, Guid? ParentId, Guid? CourseId, string? CourseTitle, string Title,
        string Body, string Kind, bool IsAnonymous, bool IsSample, string AuthorName, bool IsMine,
        bool IsOriginalPoster, int Score, int MyVote, int ReplyCount, DateTimeOffset CreatedAt);

    private static IQueryable<PostView> Views(IQueryable<CommunityPost> posts, AppDbContext db, string viewer) =>
        from post in posts
        join author in db.Users on post.AuthorId equals author.Id
        select new PostView(post.Id, post.ParentId, post.CourseId,
            db.Courses.Where(c => c.Id == post.CourseId).Select(c => c.Title).FirstOrDefault(),
            post.Title, post.Body, post.Kind, post.IsAnonymous, post.IsSample,
            post.IsAnonymous ? "Anonymous student" : author.DisplayName,
            post.AuthorId == viewer,
            post.ParentId != null && db.CommunityPosts.Any(root => root.Id == post.ParentId && root.AuthorId == post.AuthorId && root.IsAnonymous == post.IsAnonymous),
            db.CommunityVotes.Where(v => v.PostId == post.Id).Select(v => (int?)v.Value).Sum() ?? 0,
            db.CommunityVotes.Where(v => v.PostId == post.Id && v.UserId == viewer).Select(v => v.Value).FirstOrDefault(),
            db.CommunityPosts.Count(reply => reply.ParentId == post.Id), post.CreatedAt);

    public static IEndpointRouteBuilder MapCommunity(this IEndpointRouteBuilder endpoints)
    {
        var routes = endpoints.MapGroup("/api/v1/community/posts").WithTags("Community").RequireAuthorization();
        routes.MapGet("/", async (Guid? courseId, string? sort, string? kind, int? page, int? limit, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            if (sort is not (null or "recent" or "top") || kind is not (null or "Question" or "Study tip" or "Discussion")) return Results.BadRequest();
            var query = db.CommunityPosts.AsNoTracking().Where(p => p.ParentId == null);
            if (courseId is not null) query = query.Where(p => p.CourseId == courseId);
            if (kind is not null) query = query.Where(p => p.Kind == kind);
            var total = await query.CountAsync(ct);
            var current = Math.Clamp(page ?? 1, 1, 100_000);
            var size = Math.Clamp(limit ?? 15, 1, 30);
            var ordered = sort == "top" ? query.OrderByDescending(p => db.CommunityVotes.Where(v => v.PostId == p.Id).Select(v => (int?)v.Value).Sum() ?? 0).ThenByDescending(p => p.CreatedAt).ThenBy(p => p.Id)
                : query.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id);
            var data = await Views(ordered.Skip((current - 1) * size).Take(size), db,
                context.User.FindFirstValue(ClaimTypes.NameIdentifier)!).ToArrayAsync(ct);
            return Results.Ok(new { data, total, page = current, limit = size });
        });
        routes.MapGet("/{id:guid}", async (Guid id, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            var post = await Views(db.CommunityPosts.AsNoTracking().Where(p => p.Id == id && p.ParentId == null), db,
                context.User.FindFirstValue(ClaimTypes.NameIdentifier)!).SingleOrDefaultAsync(ct);
            return post is null ? Results.NotFound() : Results.Ok(post);
        });
        routes.MapGet("/{id:guid}/replies", async (Guid id, int? page, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            if (!await db.CommunityPosts.AnyAsync(p => p.Id == id && p.ParentId == null, ct)) return Results.NotFound();
            var current = Math.Clamp(page ?? 1, 1, 100_000);
            var posts = db.CommunityPosts.AsNoTracking().Where(p => p.ParentId == id);
            var total = await posts.CountAsync(ct);
            var data = await Views(posts.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id).Skip((current - 1) * 30).Take(30), db,
                context.User.FindFirstValue(ClaimTypes.NameIdentifier)!).ToArrayAsync(ct);
            return Results.Ok(new { data, total, page = current, limit = 30 });
        });
        routes.MapPost("/", (CreatePost request, HttpContext context, AppDbContext db, CancellationToken ct) =>
            CreateAsync(request, null, context, db, ct)).RequireRateLimiting("community");
        routes.MapPost("/{id:guid}/replies", (Guid id, CreateReply request, HttpContext context, AppDbContext db, CancellationToken ct) =>
            CreateAsync(new CreatePost("", request.Body, IsAnonymous: request.IsAnonymous), id, context, db, ct)).RequireRateLimiting("community");
        routes.MapPut("/{id:guid}/vote", async (Guid id, SetVote request, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            if (request.Value is < -1 or > 1) return Results.BadRequest();
            if (!await db.CommunityPosts.AnyAsync(p => p.Id == id, ct)) return Results.NotFound();
            var owner = context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            if (request.Value == 0)
                await db.CommunityVotes.Where(v => v.PostId == id && v.UserId == owner).ExecuteDeleteAsync(ct);
            else
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"CommunityVotes\" (\"PostId\", \"UserId\", \"Value\") VALUES ({id}, {owner}, {request.Value}) ON CONFLICT (\"PostId\", \"UserId\") DO UPDATE SET \"Value\" = EXCLUDED.\"Value\"", ct);
            return Results.Ok(await Views(db.CommunityPosts.AsNoTracking().Where(p => p.Id == id), db, owner).SingleAsync(ct));
        }).RequireRateLimiting("community");
        routes.MapDelete("/{id:guid}", async (Guid id, HttpContext context, AppDbContext db, CancellationToken ct) =>
        {
            var owner = context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var post = await db.CommunityPosts.SingleOrDefaultAsync(p => p.Id == id, ct);
            if (post is null || (post.AuthorId != owner && !context.User.IsInRole("Admin"))) return Results.NotFound();
            db.CommunityPosts.Remove(post);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireRateLimiting("community");
        return endpoints;
    }

    private static async Task<IResult> CreateAsync(CreatePost request, Guid? parentId, HttpContext context, AppDbContext db, CancellationToken ct)
    {
        var title = request.Title?.Trim() ?? "";
        var body = request.Body?.Trim() ?? "";
        if (body.Length is < 1 or > 8000 || (parentId is null && title.Length is < 1 or > 180)
            || request.Kind is not ("Question" or "Study tip" or "Discussion"))
            return Results.BadRequest(new { message = "Add a title up to 180 characters and a message up to 8000 characters." });
        var key = context.Request.Headers["Idempotency-Key"].ToString();
        if (key.Length is < 1 or > 100 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or ':' or '_')))
            return Results.BadRequest(new { message = "A valid Idempotency-Key is required." });
        var owner = context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var parent = parentId is null ? null : await db.CommunityPosts.AsNoTracking().SingleOrDefaultAsync(p => p.Id == parentId && p.ParentId == null, ct);
        if (parentId is not null && parent is null) return Results.NotFound();
        var courseId = parent?.CourseId ?? request.CourseId;
        if (courseId is not null && !await db.Courses.AnyAsync(c => c.Id == courseId, ct)) return Results.NotFound();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({'C' + owner}, 0))", ct);
        var existing = await db.CommunityPosts.SingleOrDefaultAsync(p => p.AuthorId == owner && p.RequestKey == key, ct);
        if (existing is not null)
        {
            if (existing.ParentId != parentId || existing.Title != title || existing.Body != body || existing.CourseId != courseId || existing.Kind != request.Kind || existing.IsAnonymous != request.IsAnonymous)
                return Results.Conflict(new { message = "This request key was already used for a different post." });
            return Results.Ok(await Views(db.CommunityPosts.Where(p => p.Id == existing.Id), db, owner).SingleAsync(ct));
        }
        var post = new CommunityPost
        {
            AuthorId = owner,
            Title = title,
            Body = body,
            Kind = request.Kind,
            CourseId = courseId,
            ParentId = parentId,
            IsAnonymous = request.IsAnonymous,
            RequestKey = key
        };
        db.CommunityPosts.Add(post);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Created($"/api/v1/community/posts/{parentId ?? post.Id}", await Views(db.CommunityPosts.Where(p => p.Id == post.Id), db, owner).SingleAsync(ct));
    }
}
