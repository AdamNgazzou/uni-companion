namespace UniCompanion.Core.Activity;

public sealed class ActivityEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EventKey { get; set; } = "";
    public string Kind { get; set; } = "";
    public Guid SourceId { get; set; }
    public string? ActorId { get; set; }
    public string[] RecipientIds { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DispatchedAt { get; set; }
}

public sealed class ActivityItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = "";
    public string GroupKey { get; set; } = "";
    public string Kind { get; set; } = "";
    public Guid SourceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

public sealed class ActivityPreference
{
    public string OwnerId { get; set; } = "";
    public bool Replies { get; set; } = true;
    public bool FollowedThreads { get; set; } = true;
    public bool StudyJobs { get; set; } = true;
    public bool Upvotes { get; set; }
}
