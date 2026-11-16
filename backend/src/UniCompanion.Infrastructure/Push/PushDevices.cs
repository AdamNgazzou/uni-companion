using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UniCompanion.Core.Push;
using UniCompanion.Core.Study;
using UniCompanion.Infrastructure.Learning;
using UniCompanion.Infrastructure.Persistence;
using UniCompanion.Infrastructure.Study;

namespace UniCompanion.Infrastructure.Push;

public sealed record DeviceRegistration(Guid InstallationId, string InstallationSecret, string Token, Guid ProjectId, string Platform, string TimeZoneId, bool Upvotes = false, long Revision = 1);
public sealed record InstallationProof(Guid InstallationId, string InstallationSecret, long Revision = 1);
public sealed class PushConfiguration(IConfiguration configuration)
{
    public Guid? ProjectId => Guid.TryParse(configuration["Push:ProjectId"], out var id) && id != Guid.Empty ? id : null;
    public bool Enabled => configuration.GetValue<bool>("Push:Enabled") && ProjectId is not null;
}
public sealed class PushDevices(AppDbContext db, IDataProtectionProvider protection, PushConfiguration configuration, TimeProvider clock)
{
    private readonly IDataProtector protector = protection.CreateProtector("UniCompanion.PushTokens.v1");
    private static readonly Regex Secret = new("^[a-fA-F0-9]{64}$", RegexOptions.CultureInvariant);
    private static readonly Regex Token = new("^(ExpoPushToken|ExponentPushToken)\\[[a-zA-Z0-9_-]{10,200}\\]$", RegexOptions.CultureInvariant);
    private static string Hash(string text) => StudyJobs.Hash(text);
    private static bool ValidProof(InstallationProof proof) => proof.InstallationId != Guid.Empty && proof.Revision is >= 1 and <= 9007199254740991 && proof.InstallationSecret is not null && Secret.IsMatch(proof.InstallationSecret);
    private static bool Matches(string hash, string secret) => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(hash), Encoding.ASCII.GetBytes(Hash(secret)));
    public string RevealToken(PushDevice device) => protector.Unprotect(device.ProtectedToken);
    public async Task InitializeAsync(string owner, InstallationProof proof, CancellationToken ct)
    {
        if (!configuration.Enabled) throw new StudyRequestException(503, "push_not_configured");
        if (!ValidProof(proof)) throw new StudyRequestException(400, "invalid_push_registration");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(427815921)", ct);
        var row = await db.PushDevices.SingleOrDefaultAsync(d => d.Id == proof.InstallationId, ct);
        if (row is not null && !Matches(row.SecretHash, proof.InstallationSecret)) throw new StudyRequestException(403, "invalid_installation_proof");
        if (row is null)
        {
            if (await db.PushDevices.CountAsync(d => d.OwnerId == owner, ct) >= 20) throw new StudyRequestException(409, "push_device_limit");
            db.PushDevices.Add(new PushDevice { Id = proof.InstallationId, OwnerId = owner, SecretHash = Hash(proof.InstallationSecret), LastSeenAt = clock.GetUtcNow() });
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
    public async Task RegisterAsync(string owner, DeviceRegistration request, CancellationToken ct)
    {
        if (!configuration.Enabled) throw new StudyRequestException(503, "push_not_configured");
        if (request.ProjectId != configuration.ProjectId || !ValidProof(new(request.InstallationId, request.InstallationSecret, request.Revision)) || request.Token is null || !Token.IsMatch(request.Token) || request.Platform is not ("android" or "ios")) throw new StudyRequestException(400, "invalid_push_registration");
        ReminderTimes.Validate(new(false, 1080, request.TimeZoneId));
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(427815921)", ct);
        // Receipt cleanup also updates this row. Lock before reading its binding so
        // a concurrent invalid-token receipt cannot overwrite a fresh registration.
        var rows = await db.PushDevices.FromSqlInterpolated($"""SELECT * FROM "PushDevices" WHERE "Id"={request.InstallationId} FOR UPDATE""").ToArrayAsync(ct);
        var row = rows.SingleOrDefault() ?? throw new StudyRequestException(409, "installation_not_initialized");
        if (!Matches(row.SecretHash, request.InstallationSecret)) throw new StudyRequestException(403, "invalid_installation_proof");
        if (request.Revision < row.CommandRevision || (request.Revision == row.CommandRevision && (row.Revoked || row.OwnerId != owner))) throw new StudyRequestException(409, "stale_installation_revision");
        var user = await db.Users.SingleAsync(u => u.Id == owner, ct); var now = clock.GetUtcNow(); var tokenHash = Hash(request.Token);
        if ((row.OwnerId != owner || !row.Enabled) && await db.PushDevices.CountAsync(d => d.OwnerId == owner && d.Enabled, ct) >= 10) throw new StudyRequestException(409, "push_device_limit");
        // A reinstall/token reassignment retires the old association; its private inbox stays intact.
        await db.PushDevices.Where(d => d.Id != row.Id && d.TokenHash == tokenHash && d.Enabled).ExecuteUpdateAsync(s => s.SetProperty(d => d.Enabled, false).SetProperty(d => d.OwnerId, (string?)null).SetProperty(d => d.ProtectedToken, "").SetProperty(d => d.Binding, d => d.Binding + 1), ct);
        var changed = !row.Enabled || row.OwnerId != owner || row.TokenHash != tokenHash || row.SecurityStamp != user.SecurityStamp;
        if (changed) { row.Binding++; row.EnabledAt = now; }
        row.OwnerId = owner; row.Enabled = true; row.TokenHash = tokenHash; row.ProtectedToken = protector.Protect(request.Token); row.SecurityStamp = user.SecurityStamp ?? "";
        row.CommandRevision = request.Revision; row.Revoked = false;
        row.Platform = request.Platform; row.TimeZoneId = request.TimeZoneId; row.Upvotes = request.Upvotes; row.LastSeenAt = now;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task RevokeAsync(InstallationProof proof, CancellationToken ct)
    {
        // Device possession authorizes unlink even after account credentials have been cleared.
        // Unknown/wrong proofs share the same response and cannot alter a registration.
        if (!ValidProof(proof)) return;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(427815921)", ct);
        var row = await db.PushDevices.SingleOrDefaultAsync(d => d.Id == proof.InstallationId, ct);
        if (row is not null && Matches(row.SecretHash, proof.InstallationSecret) && proof.Revision >= row.CommandRevision)
        {
            row.OwnerId = null; row.Enabled = false; row.ProtectedToken = ""; row.SecurityStamp = ""; row.Binding++;
            row.CommandRevision = proof.Revision; row.Revoked = true;
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }
}
