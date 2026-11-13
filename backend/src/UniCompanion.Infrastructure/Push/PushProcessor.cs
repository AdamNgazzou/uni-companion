using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniCompanion.Core.Activity;
using UniCompanion.Core.Push;
using UniCompanion.Infrastructure.Activity;
using UniCompanion.Infrastructure.Learning;
using UniCompanion.Infrastructure.Persistence;

namespace UniCompanion.Infrastructure.Push;

public sealed class PushProcessor(AppDbContext db, PushConfiguration configuration, PushDevices devices, IPushGateway gateway, TimeProvider clock)
{
    public async Task<bool> QueueNextAsync(CancellationToken ct)
    {
        if (!configuration.Enabled) return false;
        var now = clock.GetUtcNow(); var fresh = now.AddDays(-30);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(427815922)", ct);
        var candidate = await (from device in db.PushDevices.AsNoTracking()
                               join item in db.ActivityItems.AsNoTracking() on device.OwnerId equals item.OwnerId
                               where device.Enabled && device.OwnerId != null && device.LastSeenAt > fresh && item.ReadAt == null && item.UpdatedAt >= device.EnabledAt &&
                                   !db.PushDeliveries.Any(d => d.DeviceId == device.Id && d.Binding == device.Binding && d.ActivityId == item.Id && d.ActivityUpdatedAt == item.UpdatedAt)
                               orderby item.UpdatedAt, item.Id, device.Id
                               select new { Device = device, Item = item }).FirstOrDefaultAsync(ct);
        if (candidate is null) return false;
        db.PushDeliveries.Add(new PushDelivery { DeviceId = candidate.Device.Id, Binding = candidate.Device.Binding, OwnerId = candidate.Item.OwnerId, ActivityId = candidate.Item.Id, ActivityUpdatedAt = candidate.Item.UpdatedAt, CreatedAt = now, NextAt = candidate.Item.Kind == "upvote" ? now.AddMinutes(5) : now });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true;
    }
    private async Task<bool> CurrentBindingAsync(PushDelivery delivery, PushDevice? device, CancellationToken ct) => device is not null && device.Enabled && device.OwnerId == delivery.OwnerId && device.Binding == delivery.Binding && device.LastSeenAt > clock.GetUtcNow().AddDays(-30) && await db.Users.AnyAsync(u => u.Id == delivery.OwnerId && u.IsActive && u.EmailConfirmed && u.SecurityStamp == device.SecurityStamp && (u.LockoutEnd == null || u.LockoutEnd <= clock.GetUtcNow()), ct);
    private async Task<bool> EligibleAsync(PushDelivery delivery, PushDevice device, CancellationToken ct)
    {
        var admin = await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id where ur.UserId == delivery.OwnerId && role.Name == "Admin" select ur.UserId).AnyAsync(ct);
        var item = await ActivityVisibility.Query(db, delivery.OwnerId, admin).SingleOrDefaultAsync(i => i.Id == delivery.ActivityId, ct);
        if (item is null || item.ReadAt != null || item.UpdatedAt != delivery.ActivityUpdatedAt) return false;
        if (item.Kind == "revision_reminder") return await db.StudyReminders.AnyAsync(r => r.OwnerId == delivery.OwnerId && r.Enabled, ct);
        var prefs = await db.ActivityPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.OwnerId == delivery.OwnerId, ct) ?? new ActivityPreference();
        if (item.Kind.StartsWith("study_", StringComparison.Ordinal)) return prefs.StudyJobs;
        var post = await db.CommunityPosts.AsNoTracking().SingleAsync(p => p.Id == item.SourceId, ct); var rootId = post.ParentId ?? post.Id;
        if (!await db.ThreadSubscriptions.AnyAsync(s => s.ThreadId == rootId && s.OwnerId == delivery.OwnerId && s.Enabled, ct)) return false;
        if (item.Kind == "upvote") return prefs.Upvotes && device.Upvotes && !await db.ActivityEvents.AnyAsync(e => e.Kind == "upvote" && e.SourceId == post.Id && e.CreatedAt == item.UpdatedAt && db.CommunityBlocks.Any(b => b.OwnerId == delivery.OwnerId && b.BlockedUserId == e.ActorId && !b.IsAnonymous), ct);
        var rootOwner = post.ParentId is null ? post.AuthorId : await db.CommunityPosts.Where(p => p.Id == rootId).Select(p => p.AuthorId).SingleAsync(ct);
        return rootOwner == delivery.OwnerId ? prefs.Replies : prefs.FollowedThreads;
    }
    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        if (!configuration.Enabled) return false;
        var now = clock.GetUtcNow(); PushDelivery row; Guid lease;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var rows = await db.PushDeliveries.FromSqlInterpolated($"""
                SELECT * FROM "PushDeliveries" WHERE
                (("State" IN ('queued','receipt') AND "NextAt" <= {now}) OR ("State"='sending' AND "LeaseUntil" <= {now}))
                ORDER BY "NextAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
                """).ToArrayAsync(ct);
            if (rows.Length == 0) return false; row = rows[0];
            if (row.CreatedAt < now.AddHours(-24) || (row.TicketId is null && row.Attempts >= 3) || row.ReceiptChecks >= 96)
            { row.State = "failed"; row.ErrorCode = "push_budget_exceeded"; row.LeaseId = null; row.LeaseUntil = null; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return true; }
            lease = Guid.NewGuid(); row.State = "sending"; row.LeaseId = lease; row.LeaseUntil = now.AddMinutes(2);
            if (row.TicketId is null) row.Attempts++; else row.ReceiptChecks++;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        async Task Finish(string state, string? error = null, DateTimeOffset? next = null,
            string? ticket = null, bool retireDevice = false, bool refundAttempt = false)
        {
            // Publication owns the delivery row first. Device invalidation must commit
            // with that publication, never as a side effect of a stale provider response.
            await using var transaction = await db.Database.BeginTransactionAsync(CancellationToken.None);
            var changed = await Owned(row.Id, lease).ExecuteUpdateAsync(s => s
                .SetProperty(d => d.State, state).SetProperty(d => d.ErrorCode, error)
                .SetProperty(d => d.NextAt, next ?? clock.GetUtcNow()).SetProperty(d => d.TicketId, ticket)
                .SetProperty(d => d.Attempts, d => refundAttempt ? d.Attempts - 1 : d.Attempts)
                .SetProperty(d => d.LeaseId, (Guid?)null).SetProperty(d => d.LeaseUntil, (DateTimeOffset?)null), CancellationToken.None);
            if (changed == 1 && retireDevice)
                await db.PushDevices.Where(d => d.Id == row.DeviceId && d.Binding == row.Binding)
                    .ExecuteUpdateAsync(s => s.SetProperty(d => d.Enabled, false).SetProperty(d => d.ProtectedToken, "")
                        .SetProperty(d => d.Binding, d => d.Binding + 1), CancellationToken.None);
            await transaction.CommitAsync(CancellationToken.None);
        }

        try
        {
            var device = await db.PushDevices.AsNoTracking().SingleOrDefaultAsync(d => d.Id == row.DeviceId, ct);
            if (!await CurrentBindingAsync(row, device, ct)) { await Finish("cancelled", "binding_unavailable"); return true; }
            if (row.TicketId is null)
            {
                if (!await EligibleAsync(row, device!, ct)) { await Finish("cancelled", "activity_unavailable"); return true; }
                var quiet = await db.StudyReminders.AsNoTracking().SingleOrDefaultAsync(r => r.OwnerId == row.OwnerId, ct);
                var zoneId = quiet?.TimeZoneId ?? device!.TimeZoneId; var start = quiet?.QuietStart ?? 1320; var end = quiet?.QuietEnd ?? 420;
                var local = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(zoneId));
                if (ReminderTimes.IsQuiet(local.Hour * 60 + local.Minute, start, end))
                {
                    // Recheck periodically, including timezone/DST changes. Waiting is not a send attempt.
                    await Finish("queued", next: now.AddMinutes(5), refundAttempt: true); return true;
                }
            }
            if (!await Owned(row.Id, lease).AnyAsync(ct)) return true;
            var outcome = row.TicketId is { } id ? await gateway.ReceiptAsync(id, ct) : await gateway.SendAsync(devices.RevealToken(device!), row.ActivityId, ct);
            if (outcome.Status == "accepted" && outcome.TicketId is { Length: > 0 and <= 200 }) await Finish("receipt", next: now.AddMinutes(15), ticket: outcome.TicketId);
            else if (outcome.Status == "delivered") await Finish("done");
            else if (outcome.Status == "pending") await Finish("receipt", next: now.AddMinutes(15), ticket: row.TicketId);
            else
            {
                // A confirmed delivery failure may be resent. A failed receipt HTTP
                // lookup is handled below and retains its existing ticket instead.
                await Finish(outcome.Retryable && row.Attempts < 3 ? "queued" : "failed",
                    outcome.ErrorCode ?? "invalid_push_response", now.AddMinutes(1 << Math.Min(row.Attempts, 6)),
                    retireDevice: outcome.ErrorCode == "DeviceNotRegistered");
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { await Finish(row.TicketId is null ? "queued" : "receipt", "worker_restarting", ticket: row.TicketId); }
        catch (Exception error) when (error is PushTransportException or HttpRequestException or OperationCanceledException or JsonException or KeyNotFoundException or InvalidOperationException or CryptographicException)
        {
            var retry = error is not PushTransportException transport || transport.Retryable;
            if (error is CryptographicException) retry = false;
            await Finish(retry && (row.TicketId is not null || row.Attempts < 3) ? row.TicketId is null ? "queued" : "receipt" : "failed", "push_transport_unavailable", now.AddMinutes(2), row.TicketId);
        }
        return true;
    }
    private IQueryable<PushDelivery> Owned(Guid deliveryId, Guid leaseId) => db.PushDeliveries.Where(d =>
        d.Id == deliveryId && d.State == "sending" && d.LeaseId == leaseId && d.LeaseUntil > clock.GetUtcNow());

}
