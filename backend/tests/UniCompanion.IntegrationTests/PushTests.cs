using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UniCompanion.Core.Activity;
using UniCompanion.Core.Push;
using UniCompanion.Core.Study;
using UniCompanion.Infrastructure.Learning;
using UniCompanion.Infrastructure.Persistence;
using UniCompanion.Infrastructure.Push;
using Xunit;

namespace UniCompanion.IntegrationTests;

public sealed class TestPushGateway : IPushGateway
{
    public ConcurrentQueue<(string Token, Guid Activity)> Sent { get; } = new();
    public Func<Task<PushOutcome>> Send { get; set; } = () => Task.FromResult(new PushOutcome("accepted", "test-ticket"));
    public Func<Task<PushOutcome>> Receipt { get; set; } = () => Task.FromResult(new PushOutcome("delivered"));
    public Task<PushOutcome> SendAsync(string token, Guid activityId, CancellationToken ct) { Sent.Enqueue((token, activityId)); return Send(); }
    public Task<PushOutcome> ReceiptAsync(string ticketId, CancellationToken ct) => Receipt();
    public void Reset() { Sent.Clear(); Send = () => Task.FromResult(new PushOutcome("accepted", "test-ticket")); Receipt = () => Task.FromResult(new PushOutcome("delivered")); }
}
public sealed class PushTests(ApiFixture app) : IClassFixture<ApiFixture>
{
    private static readonly Guid Project = Guid.Parse("d871a33f-d3d4-456d-8509-76dbb631a762");
    private const string Token = "ExpoPushToken[private_fixture_token_123456789]";
    private async Task<(HttpClient Client, string Owner, Guid Course, DeviceRegistration Registration)> SeedAsync()
    {
        app.Push.Reset(); var config = app.Services.GetRequiredService<IConfiguration>(); config["Push:Enabled"] = "true"; config["Push:ProjectId"] = Project.ToString();
        var user = await app.RegisterAsync(); var client = app.AsUser(user.Access); var owner = (await client.GetFromJsonAsync<JsonElement>("/api/v1/accounts/me")).GetProperty("id").GetString()!;
        var course = (await (await client.PostAsJsonAsync("/api/v1/courses/", new { title = "Push private course", description = "" })).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await using var scope = app.Services.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<RevisionReminders>().SaveAsync(owner, new(false, 1080, "UTC", 0, 0), CancellationToken.None);
        var registration = new DeviceRegistration(Guid.NewGuid(), new string('a', 64), Token, Project, "android", "UTC");
        (await client.PostAsJsonAsync("/api/v1/devices/push/initialize", new InstallationProof(registration.InstallationId, registration.InstallationSecret))).EnsureSuccessStatusCode();
        return (client, owner, course, registration);
    }
    private async Task<Guid> Source(string owner, Guid course)
    {
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var job = new StudyJob { OwnerId = owner, CourseId = course, Kind = "summary", State = JobStates.Succeeded };
        var item = new ActivityItem { OwnerId = owner, Kind = "study_completed", SourceId = job.Id, GroupKey = "study:" + job.Id, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        db.Add(job); db.Add(item); await db.SaveChangesAsync(); return item.Id;
    }
    private async Task<bool> Queue() { await using var scope = app.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<PushProcessor>().QueueNextAsync(CancellationToken.None); }
    private async Task<bool> Process() { await using var scope = app.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<PushProcessor>().ProcessNextAsync(CancellationToken.None); }
    private async Task Due(Guid device)
    {
        await using var scope = app.Services.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<AppDbContext>().PushDeliveries.Where(d => d.DeviceId == device && (d.State == "queued" || d.State == "receipt")).ExecuteUpdateAsync(s => s.SetProperty(d => d.NextAt, DateTimeOffset.UtcNow.AddSeconds(-1)));
    }
    private static async Task<Guid> Post(HttpClient client, string path, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1" + path) { Content = JsonContent.Create(body) }; request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await client.SendAsync(request); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    private async Task DispatchActivity()
    {
        await using var scope = app.Services.CreateAsyncScope(); Assert.True(await scope.ServiceProvider.GetRequiredService<UniCompanion.Infrastructure.Activity.ActivityDispatcher>().DispatchNextAsync(CancellationToken.None));
    }
    [Fact]
    public async Task InstallationProofProtectsRegistrationAndCredentialFreeUnlinkAndTokenIsEncrypted()
    {
        var (client, owner, _, registration) = await SeedAsync(); using var writer = client;
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.PostAsJsonAsync("/api/v1/devices/push/register", registration)).StatusCode);
        (await writer.PostAsJsonAsync("/api/v1/devices/push/register", registration)).EnsureSuccessStatusCode();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); var row = await db.PushDevices.SingleAsync(d => d.Id == registration.InstallationId);
            Assert.Equal(owner, row.OwnerId); Assert.DoesNotContain(Token, row.ProtectedToken); Assert.DoesNotContain(registration.InstallationSecret, row.SecretHash); Assert.Equal(Token, scope.ServiceProvider.GetRequiredService<PushDevices>().RevealToken(row));
        }
        var otherAccount = await app.RegisterAsync(); using var other = app.AsUser(otherAccount.Access); var wrong = new string('b', 64);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.PostAsJsonAsync("/api/v1/devices/push/register", registration with { InstallationSecret = wrong })).StatusCode);
        (await app.Client.PostAsJsonAsync("/api/v1/devices/push/revoke", new InstallationProof(registration.InstallationId, wrong))).EnsureSuccessStatusCode();
        await using (var scope = app.Services.CreateAsyncScope()) Assert.True(await scope.ServiceProvider.GetRequiredService<AppDbContext>().PushDevices.AnyAsync(d => d.Id == registration.InstallationId && d.OwnerId == owner && d.Enabled));
        (await app.Client.PostAsJsonAsync("/api/v1/devices/push/revoke", new InstallationProof(registration.InstallationId, registration.InstallationSecret))).EnsureSuccessStatusCode();
        await using (var scope = app.Services.CreateAsyncScope()) { var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().PushDevices.SingleAsync(d => d.Id == registration.InstallationId); Assert.False(row.Enabled); Assert.Null(row.OwnerId); Assert.Empty(row.ProtectedToken); }
    }
    [Fact]
    public async Task QueueDeduplicatesAndIgnoresHistoricalReadOrDisabledBindingActivity()
    {
        var (client, owner, course, registration) = await SeedAsync(); using var writer = client;
        await Source(owner, course); (await writer.PostAsJsonAsync("/api/v1/devices/push/register", registration)).EnsureSuccessStatusCode(); Assert.False(await Queue());
        var id = await Source(owner, course); Assert.Single(await Task.WhenAll(Queue(), Queue()), value => value); Assert.True(await Process()); Assert.Single(app.Push.Sent); Assert.Equal(id, app.Push.Sent.Single().Activity); Assert.False(await Queue());
        await Due(registration.InstallationId); Assert.True(await Process()); Assert.Single(app.Push.Sent);
        await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); Assert.Equal("done", (await db.PushDeliveries.SingleAsync(d => d.ActivityId == id)).State);
    }
    [Fact]
    public async Task RevocationAndPreferenceChangesCancelBeforeAnySend()
    {
        var (client, owner, course, registration) = await SeedAsync(); using var writer = client;
        (await writer.PostAsJsonAsync("/api/v1/devices/push/register", registration)).EnsureSuccessStatusCode(); await Source(owner, course); Assert.True(await Queue());
        await using (var scope = app.Services.CreateAsyncScope()) { var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); db.ActivityPreferences.Add(new ActivityPreference { OwnerId = owner, StudyJobs = false }); await db.SaveChangesAsync(); }
        Assert.True(await Process()); Assert.Empty(app.Push.Sent);
        await using (var scope = app.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<AppDbContext>().ActivityPreferences.Where(p => p.OwnerId == owner).ExecuteUpdateAsync(s => s.SetProperty(p => p.StudyJobs, true));
        await Source(owner, course); Assert.True(await Queue());
        await using (var scope = app.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<AppDbContext>().CourseMemberships.Where(m => m.CourseId == course && m.UserId == owner).ExecuteDeleteAsync();
        Assert.True(await Process()); Assert.Empty(app.Push.Sent);
    }
    [Fact]
    public async Task RotationAndAccountSwitchFenceQueuedSendsAndOldReceipts()
    {
        var (client, owner, course, registration) = await SeedAsync(); using var writer = client;
        (await writer.PostAsJsonAsync("/api/v1/devices/push/register", registration)).EnsureSuccessStatusCode(); await Source(owner, course); Assert.True(await Queue()); Assert.True(await Process());
        var rotated = registration with { Token = "ExpoPushToken[rotated_fixture_token_123456789]", Revision = 2 }; (await writer.PostAsJsonAsync("/api/v1/devices/push/register", rotated)).EnsureSuccessStatusCode();
        app.Push.Receipt = () => Task.FromResult(new PushOutcome("error", ErrorCode: "DeviceNotRegistered")); await Due(registration.InstallationId); Assert.True(await Process());
        await using (var scope = app.Services.CreateAsyncScope()) Assert.True(await scope.ServiceProvider.GetRequiredService<AppDbContext>().PushDevices.AnyAsync(d => d.Id == registration.InstallationId && d.Enabled));
        await Source(owner, course); Assert.True(await Queue());
        var second = await app.RegisterAsync(); using var other = app.AsUser(second.Access); (await other.PostAsJsonAsync("/api/v1/devices/push/register", rotated with { Revision = 3 })).EnsureSuccessStatusCode();
        Assert.True(await Process()); Assert.Single(app.Push.Sent);
    }
    [Fact]
    public async Task RetryRecoversAndCurrentDeviceNotRegisteredReceiptDisablesOnlyItsBinding()
    {
        var (client, owner, course, registration) = await SeedAsync(); using var writer = client;
        (await writer.PostAsJsonAsync("/api/v1/devices/push/register", registration)).EnsureSuccessStatusCode(); await Source(owner, course); Assert.True(await Queue());
        app.Push.Send = () => throw new PushTransportException(true); Assert.True(await Process());
        await Due(registration.InstallationId); app.Push.Send = () => Task.FromResult(new PushOutcome("accepted", "recovered-ticket")); Assert.True(await Process()); Assert.Equal(2, app.Push.Sent.Count);
        app.Push.Receipt = () => Task.FromResult(new PushOutcome("error", ErrorCode: "DeviceNotRegistered")); await Due(registration.InstallationId); Assert.True(await Process());
        await using var scope = app.Services.CreateAsyncScope(); Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().PushDevices.AnyAsync(d => d.Id == registration.InstallationId && d.Enabled));
    }
    [Fact]
    public async Task WrongProjectAndMissingConfigurationRefuseTokenRegistration()
    {
        var (client, _, _, registration) = await SeedAsync(); using var writer = client;
        Assert.Equal(HttpStatusCode.BadRequest, (await writer.PostAsJsonAsync("/api/v1/devices/push/register", registration with { ProjectId = Guid.NewGuid() })).StatusCode);
        var config = app.Services.GetRequiredService<IConfiguration>(); config["Push:Enabled"] = "false";
        Assert.False((await writer.GetFromJsonAsync<JsonElement>("/api/v1/devices/push/status")).GetProperty("available").GetBoolean());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await writer.PostAsJsonAsync("/api/v1/devices/push/register", registration)).StatusCode);
    }
    [Fact]
    public async Task AnonymousUnlinkCannotCreateRowsAndInitializationRequiresAnAccount()
    {
        var (client, _, _, registration) = await SeedAsync(); using var writer = client;
        var unknown = new InstallationProof(Guid.NewGuid(), new string('c', 64), 2);
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.PostAsJsonAsync("/api/v1/devices/push/initialize", unknown)).StatusCode);
        (await app.Client.PostAsJsonAsync("/api/v1/devices/push/revoke", unknown)).EnsureSuccessStatusCode();
        await using var scope = app.Services.CreateAsyncScope(); Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().PushDevices.AnyAsync(d => d.Id == unknown.InstallationId));
        Assert.Equal(HttpStatusCode.Conflict, (await writer.PostAsJsonAsync("/api/v1/devices/push/register", registration with { InstallationId = unknown.InstallationId, InstallationSecret = unknown.InstallationSecret })).StatusCode);
    }
    [Fact]
    public async Task UnlinkTombstoneRejectsLateRegistrationAndOldUnlinkCannotRetireANewerAccount()
    {
        var (client, _, _, registration) = await SeedAsync(); using var writer = client;
        var proof = new InstallationProof(registration.InstallationId, registration.InstallationSecret, 2);
        (await app.Client.PostAsJsonAsync("/api/v1/devices/push/revoke", proof)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await writer.PostAsJsonAsync("/api/v1/devices/push/register", registration)).StatusCode);
        var second = await app.RegisterAsync(); using var other = app.AsUser(second.Access); var otherId = (await other.GetFromJsonAsync<JsonElement>("/api/v1/accounts/me")).GetProperty("id").GetString();
        (await other.PostAsJsonAsync("/api/v1/devices/push/register", registration with { Revision = 3 })).EnsureSuccessStatusCode();
        (await app.Client.PostAsJsonAsync("/api/v1/devices/push/revoke", proof)).EnsureSuccessStatusCode();
        await using var scope = app.Services.CreateAsyncScope(); Assert.True(await scope.ServiceProvider.GetRequiredService<AppDbContext>().PushDevices.AnyAsync(d => d.Id == registration.InstallationId && d.OwnerId == otherId && d.Enabled));
    }
    [Fact]
    public async Task ReceiptArrivingAfterTokenRotationCannotDisableTheNewBinding()
    {
        var (client, owner, course, registration) = await SeedAsync(); using var writer = client;
        (await writer.PostAsJsonAsync("/api/v1/devices/push/register", registration)).EnsureSuccessStatusCode(); await Source(owner, course); Assert.True(await Queue()); Assert.True(await Process());
        var rotated = registration with { Revision = 2, Token = "ExpoPushToken[new_receipt_race_token_123456789]" };
        app.Push.Receipt = async () => { (await writer.PostAsJsonAsync("/api/v1/devices/push/register", rotated)).EnsureSuccessStatusCode(); return new PushOutcome("error", ErrorCode: "DeviceNotRegistered"); };
        await Due(registration.InstallationId); Assert.True(await Process());
        await using var scope = app.Services.CreateAsyncScope(); var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().PushDevices.SingleAsync(d => d.Id == registration.InstallationId); Assert.True(row.Enabled); Assert.Equal(rotated.Token, scope.ServiceProvider.GetRequiredService<PushDevices>().RevealToken(row));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StaleReceiptCannotRetireDeviceOrOverwriteTheCurrentDelivery(bool leaseReplaced)
    {
        var (client, owner, course, registration) = await SeedAsync(); using var writer = client;
        (await writer.PostAsJsonAsync("/api/v1/devices/push/register", registration)).EnsureSuccessStatusCode();
        var activity = await Source(owner, course); Assert.True(await Queue()); Assert.True(await Process());
        var replacement = Guid.NewGuid();
        app.Push.Receipt = async () =>
        {
            await using var scope = app.Services.CreateAsyncScope();
            var query = scope.ServiceProvider.GetRequiredService<AppDbContext>().PushDeliveries.Where(d => d.ActivityId == activity);
            if (leaseReplaced)
                await query.ExecuteUpdateAsync(s => s.SetProperty(d => d.LeaseId, replacement).SetProperty(d => d.LeaseUntil, DateTimeOffset.UtcNow.AddMinutes(2)));
            else await query.ExecuteUpdateAsync(s => s.SetProperty(d => d.LeaseUntil, DateTimeOffset.UtcNow.AddSeconds(-1)));
            return new PushOutcome("error", ErrorCode: "DeviceNotRegistered");
        };
        try
        {
            await Due(registration.InstallationId); Assert.True(await Process());
            await using var scope = app.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True((await db.PushDevices.SingleAsync(d => d.Id == registration.InstallationId)).Enabled);
            var delivery = await db.PushDeliveries.SingleAsync(d => d.ActivityId == activity);
            Assert.Equal("sending", delivery.State); Assert.Equal("test-ticket", delivery.TicketId);
            if (leaseReplaced) Assert.Equal(replacement, delivery.LeaseId);
        }
        finally
        {
            await using var scope = app.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().PushDeliveries.Where(d => d.ActivityId == activity).ExecuteUpdateAsync(s => s.SetProperty(d => d.State, "done"));
        }
    }

    [Fact]
    public async Task ExpiredSendCannotPublishAnAcceptedTicket()
    {
        var (client, owner, course, registration) = await SeedAsync(); using var writer = client;
        (await writer.PostAsJsonAsync("/api/v1/devices/push/register", registration)).EnsureSuccessStatusCode();
        var activity = await Source(owner, course); Assert.True(await Queue());
        app.Push.Send = async () =>
        {
            await using var scope = app.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().PushDeliveries.Where(d => d.ActivityId == activity).ExecuteUpdateAsync(s => s.SetProperty(d => d.LeaseUntil, DateTimeOffset.UtcNow.AddSeconds(-1)));
            return new PushOutcome("accepted", "late-ticket");
        };
        try
        {
            Assert.True(await Process());
            await using var scope = app.Services.CreateAsyncScope();
            var delivery = await scope.ServiceProvider.GetRequiredService<AppDbContext>().PushDeliveries.SingleAsync(d => d.ActivityId == activity);
            Assert.Equal("sending", delivery.State); Assert.Null(delivery.TicketId);
        }
        finally
        {
            await using var scope = app.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().PushDeliveries.Where(d => d.ActivityId == activity).ExecuteUpdateAsync(s => s.SetProperty(d => d.State, "done"));
        }
    }

    [Fact]
    public async Task ReceiptLookupOutageRetainsTicketButConfirmedDeliveryFailureRetriesTheSend()
    {
        var (client, owner, course, registration) = await SeedAsync(); using var writer = client;
        (await writer.PostAsJsonAsync("/api/v1/devices/push/register", registration)).EnsureSuccessStatusCode();
        var activity = await Source(owner, course); Assert.True(await Queue()); Assert.True(await Process());
        app.Push.Receipt = () => throw new HttpRequestException("Fixture transport outage");
        await Due(registration.InstallationId); Assert.True(await Process());
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var row = await scope.ServiceProvider.GetRequiredService<AppDbContext>().PushDeliveries.SingleAsync(d => d.ActivityId == activity);
            Assert.Equal("receipt", row.State); Assert.Equal("test-ticket", row.TicketId); Assert.Equal(1, row.Attempts);
        }
        app.Push.Receipt = () => Task.FromResult(new PushOutcome("error", ErrorCode: "MessageRateExceeded", Retryable: true));
        await Due(registration.InstallationId); Assert.True(await Process());
        await Due(registration.InstallationId); Assert.True(await Process());
        Assert.Equal(2, app.Push.Sent.Count);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    [Fact]
    public async Task QuietHoursWaitWithoutSpendingSendAttemptsAndChangedSecurityStampRefusesSend()
    {
        var (client, owner, course, registration) = await SeedAsync(); using var writer = client;
        (await writer.PostAsJsonAsync("/api/v1/devices/push/register", registration)).EnsureSuccessStatusCode(); await Source(owner, course); Assert.True(await Queue());
        var night = new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date.AddHours(23), TimeSpan.Zero);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>(); await db.StudyReminders.Where(r => r.OwnerId == owner).ExecuteUpdateAsync(s => s.SetProperty(r => r.QuietStart, 1320).SetProperty(r => r.QuietEnd, 420));
            // The synthetic clock may be before today's real queue time. Keep this
            // fixture due and within budget regardless of the UTC hour CI runs in.
            await db.PushDeliveries.Where(d => d.DeviceId == registration.InstallationId).ExecuteUpdateAsync(s =>
                s.SetProperty(d => d.CreatedAt, night.AddMinutes(-1)).SetProperty(d => d.NextAt, night.AddSeconds(-1)));
            var processor = new PushProcessor(db, scope.ServiceProvider.GetRequiredService<PushConfiguration>(), scope.ServiceProvider.GetRequiredService<PushDevices>(), app.Push, new FixedClock(night)); Assert.True(await processor.ProcessNextAsync(CancellationToken.None));
            db.ChangeTracker.Clear(); var delivery = await db.PushDeliveries.SingleAsync(d => d.DeviceId == registration.InstallationId); Assert.Equal("queued", delivery.State); Assert.Equal(0, delivery.Attempts); Assert.Empty(app.Push.Sent);
            await db.Users.Where(u => u.Id == owner).ExecuteUpdateAsync(s => s.SetProperty(u => u.SecurityStamp, Guid.NewGuid().ToString()));
        }
        await Due(registration.InstallationId); Assert.True(await Process()); Assert.Empty(app.Push.Sent);
    }
    [Fact]
    public async Task LiveThreadMuteAndIdentityModeBlockAreRecheckedBeforePush()
    {
        var (client, _, _, registration) = await SeedAsync(); using var writer = client;
        (await writer.PostAsJsonAsync("/api/v1/devices/push/register", registration)).EnsureSuccessStatusCode();
        var root = await Post(writer, "/community/posts", new { title = "Push question", body = "Explain a labelled mapping", kind = "Question", isAnonymous = false });
        var otherAccount = await app.RegisterAsync(); using var other = app.AsUser(otherAccount.Access);
        await Post(other, $"/community/posts/{root}/replies", new { body = "A first named reply", isAnonymous = false }); await DispatchActivity(); Assert.True(await Queue());
        (await writer.PutAsJsonAsync($"/api/v1/community/posts/{root}/subscription", new { following = false })).EnsureSuccessStatusCode(); Assert.True(await Process()); Assert.Empty(app.Push.Sent);
        (await writer.PutAsJsonAsync($"/api/v1/community/posts/{root}/subscription", new { following = true })).EnsureSuccessStatusCode();
        var named = await Post(other, $"/community/posts/{root}/replies", new { body = "A second named reply", isAnonymous = false }); await DispatchActivity(); Assert.True(await Queue());
        (await writer.PostAsync($"/api/v1/community/posts/{named}/block", null)).EnsureSuccessStatusCode(); Assert.True(await Process()); Assert.Empty(app.Push.Sent);
        await Post(other, $"/community/posts/{root}/replies", new { body = "A distinct anonymous reply", isAnonymous = true }); await DispatchActivity(); Assert.True(await Queue()); Assert.True(await Process()); Assert.Single(app.Push.Sent);
    }
}
