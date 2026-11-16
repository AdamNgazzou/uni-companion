using Microsoft.EntityFrameworkCore;
using UniCompanion.Core.Activity;
using UniCompanion.Core.Learning;
using UniCompanion.Core.Study;
using UniCompanion.Infrastructure.Persistence;

namespace UniCompanion.Infrastructure.Learning;

public sealed record ReminderRequest(bool Enabled, int MinuteOfDay, string TimeZoneId, int QuietStart = 1320, int QuietEnd = 420);
public static class ReminderTimes
{
    public static bool IsQuiet(int minute, int start, int end) => start == end ? false : start < end ? minute >= start && minute < end : minute >= start || minute < end;
    public static TimeZoneInfo Validate(ReminderRequest request)
    {
        if (request.MinuteOfDay is < 0 or > 1439 || request.QuietStart is < 0 or > 1439 || request.QuietEnd is < 0 or > 1439 || string.IsNullOrWhiteSpace(request.TimeZoneId) || request.TimeZoneId.Length > 100) throw new StudyRequestException(400, "invalid_reminder");
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { throw new StudyRequestException(400, "invalid_timezone"); }
        if (request.Enabled && IsQuiet(request.MinuteOfDay, request.QuietStart, request.QuietEnd)) throw new StudyRequestException(400, "reminder_in_quiet_hours");
        return zone;
    }
    public static DateTimeOffset Next(DateTimeOffset after, ReminderRequest request)
    {
        var zone = Validate(request); var date = TimeZoneInfo.ConvertTime(after, zone).Date;
        for (var day = 0; day < 370; day++)
        {
            var local = DateTime.SpecifyKind(date.AddDays(day).AddMinutes(request.MinuteOfDay), DateTimeKind.Unspecified);
            // Spring gaps move forward to the first valid, non-quiet minute. Fall folds
            // use the earlier occurrence once; the next run advances to the next local day.
            for (var minute = 0; minute < 1440 && (zone.IsInvalidTime(local) || IsQuiet(local.Hour * 60 + local.Minute, request.QuietStart, request.QuietEnd)); minute++) local = local.AddMinutes(1);
            var offset = zone.IsAmbiguousTime(local) ? zone.GetAmbiguousTimeOffsets(local).Max() : zone.GetUtcOffset(local);
            var candidate = new DateTimeOffset(local, offset).ToUniversalTime();
            if (candidate > after) return candidate;
        }
        throw new StudyRequestException(400, "invalid_reminder");
    }
}
public sealed class RevisionReminders(AppDbContext db, TimeProvider clock)
{
    public async Task<StudyReminder> SaveAsync(string owner, ReminderRequest request, CancellationToken ct)
    {
        ReminderTimes.Validate(request);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"reminder:" + owner},0))", ct);
        var row = await db.StudyReminders.SingleOrDefaultAsync(s => s.OwnerId == owner, ct);
        if (row is null) { row = new StudyReminder { OwnerId = owner }; db.StudyReminders.Add(row); }
        row.Enabled = request.Enabled; row.MinuteOfDay = request.MinuteOfDay; row.TimeZoneId = request.TimeZoneId; row.QuietStart = request.QuietStart; row.QuietEnd = request.QuietEnd;
        row.NextDueAt = request.Enabled ? ReminderTimes.Next(clock.GetUtcNow(), request) : null;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return row;
    }
    public async Task<bool> DispatchNextAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.StudyReminders.FromSqlInterpolated($"""SELECT * FROM "StudyReminders" WHERE "Enabled" AND "NextDueAt" <= {now} ORDER BY "NextDueAt", "OwnerId" LIMIT 1 FOR UPDATE SKIP LOCKED""").ToArrayAsync(ct);
        if (rows.Length == 0) return false;
        var row = rows[0]; var due = row.NextDueAt!.Value;
        var local = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(row.TimeZoneId));
        if (!ReminderTimes.IsQuiet(local.Hour * 60 + local.Minute, row.QuietStart, row.QuietEnd) && await db.Users.AnyAsync(u => u.Id == row.OwnerId && u.IsActive && u.EmailConfirmed && (u.LockoutEnd == null || u.LockoutEnd <= now), ct))
        {
            var key = "revision_reminder:" + row.Id + ":" + due.ToUnixTimeSeconds();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "ActivityItems" ("Id","OwnerId","GroupKey","Kind","SourceId","CreatedAt","UpdatedAt")
                VALUES ({Guid.NewGuid()},{row.OwnerId},{key},'revision_reminder',{row.Id},{now},{now})
                ON CONFLICT ("OwnerId","GroupKey") DO NOTHING
                """, ct);
        }
        // Catch up with one useful reminder, rather than replaying missed days after downtime.
        row.NextDueAt = ReminderTimes.Next(now, new ReminderRequest(true, row.MinuteOfDay, row.TimeZoneId, row.QuietStart, row.QuietEnd));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }
}
