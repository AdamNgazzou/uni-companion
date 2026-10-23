namespace UniCompanion.Core.Community;

public sealed class CommunityPost
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ParentId { get; set; }
    public Guid? CourseId { get; set; }
    public string AuthorId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Kind { get; set; } = "Discussion";
    public bool IsAnonymous { get; set; }
    public bool IsSample { get; set; }
    public string RequestKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public DateTimeOffset? EditedAt { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? AcceptedReplyId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ThreadSubscription
{
    public Guid ThreadId { get; set; }
    public string OwnerId { get; set; } = "";
    public bool Enabled { get; set; } = true;
}

public sealed class SavedItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = "";
    public string Kind { get; set; } = "";
    public Guid TargetId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CommunityBlock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = "";
    public string BlockedUserId { get; set; } = "";
    public bool IsAnonymous { get; set; }
}

public sealed class CommunityReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PostId { get; set; }
    public string ReporterId { get; set; } = "";
    public string Reason { get; set; } = "";
    public string State { get; set; } = "open";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CommunityVote
{
    public Guid PostId { get; set; }
    public string UserId { get; set; } = "";
    public int Value { get; set; }
}
