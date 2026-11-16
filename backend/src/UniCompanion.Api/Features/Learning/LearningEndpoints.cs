using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using UniCompanion.Core.Learning;
using UniCompanion.Infrastructure.Learning;
using UniCompanion.Infrastructure.Persistence;

namespace UniCompanion.Api.Features.Learning;

public static class LearningEndpoints
{
    private static string Owner(HttpContext context) => context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public static IEndpointRouteBuilder MapLearning(this IEndpointRouteBuilder endpoints)
    {
        var routes = endpoints.MapGroup("/api/v1/learning").RequireAuthorization().WithTags("Learning");
        routes.MapGet("/progress", async (HttpContext context, LearningProgress progress, CancellationToken ct) => Results.Ok(await progress.CountsAsync(Owner(context), ct)));
        routes.MapGet("/revision", async (string? state, string? q, int? page, HttpContext context, LearningProgress progress, CancellationToken ct) =>
        {
            var term = q?.Trim() ?? ""; var filter = state ?? "active";
            if (filter is not ("active" or "snoozed" or "dismissed") || term.Length > 100) return Results.BadRequest();
            var all = await progress.CardsAsync(Owner(context), ct);
            var rows = all.Where(c => c.State == filter && (term.Length == 0 || (c.Title + " " + c.Topic + " " + c.Note + " " + c.CourseTitle).Contains(term, StringComparison.OrdinalIgnoreCase))).ToArray();
            var number = Math.Clamp(page ?? 1, 1, 10000);
            return Results.Ok(new { data = rows.Skip((number - 1) * 20).Take(20), total = rows.Length, dueCount = all.Count(c => c.State == "active"), page = number, limit = 20 });
        });
        routes.MapPost("/revision", async (ConceptRequest request, HttpContext context, LearningProgress progress, CancellationToken ct) => Results.Ok(new { key = await progress.CreateAsync(Owner(context), context.Request.Headers["Idempotency-Key"].ToString(), request, ct) })).RequireRateLimiting("community");
        routes.MapPut("/revision/{key}", async (string key, RevisionUpdate request, HttpContext context, LearningProgress progress, CancellationToken ct) => { await progress.UpdateAsync(Owner(context), key, request, ct); return Results.NoContent(); }).RequireRateLimiting("community");
        routes.MapGet("/reminder", async (HttpContext context, AppDbContext db, CancellationToken ct) => Results.Ok(await db.StudyReminders.AsNoTracking().SingleOrDefaultAsync(s => s.OwnerId == Owner(context), ct) ?? new StudyReminder()));
        routes.MapPut("/reminder", async (ReminderRequest request, HttpContext context, RevisionReminders reminders, CancellationToken ct) =>
        {
            var row = await reminders.SaveAsync(Owner(context), request, ct); return Results.Ok(new { row.Enabled, row.MinuteOfDay, row.TimeZoneId, row.QuietStart, row.QuietEnd, row.NextDueAt });
        }).RequireRateLimiting("community");
        return endpoints;
    }
}
