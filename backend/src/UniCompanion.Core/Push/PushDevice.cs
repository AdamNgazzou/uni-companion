namespace UniCompanion.Core.Push;

public sealed class PushDevice
{
    public Guid Id { get; set; }
    public string? OwnerId { get; set; }
    public string SecretHash { get; set; } = "";
    public string ProtectedToken { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public string SecurityStamp { get; set; } = "";
    public string Platform { get; set; } = "";
    public string TimeZoneId { get; set; } = "UTC";
    public bool Enabled { get; set; }
    public bool Upvotes { get; set; }
    public long Binding { get; set; }
    public long CommandRevision { get; set; }
    public bool Revoked { get; set; }
    public DateTimeOffset EnabledAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}
public sealed class PushDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeviceId { get; set; }
    public long Binding { get; set; }
    public string OwnerId { get; set; } = "";
    public Guid ActivityId { get; set; }
    public DateTimeOffset ActivityUpdatedAt { get; set; }
    public string State { get; set; } = "queued";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset NextAt { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public int Attempts { get; set; }
    public int ReceiptChecks { get; set; }
    public string? TicketId { get; set; }
    public string? ErrorCode { get; set; }
}
public sealed record PushOutcome(string Status, string? TicketId = null, string? ErrorCode = null, bool Retryable = false);
public interface IPushGateway
{
    Task<PushOutcome> SendAsync(string token, Guid activityId, CancellationToken ct);
    Task<PushOutcome> ReceiptAsync(string ticketId, CancellationToken ct);
}
