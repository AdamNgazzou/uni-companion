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
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CommunityVote
{
    public Guid PostId { get; set; }
    public string UserId { get; set; } = "";
    public int Value { get; set; }
}
