namespace UniCompanion.Core.Practice;

public sealed class PracticeProgress
{
    public Guid JobId { get; set; }
    public string OwnerId { get; set; } = "";
    public string TaskId { get; set; } = "";
    public int HintLevel { get; set; }
    public DateTimeOffset? RevealedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
public sealed class PracticeAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public string OwnerId { get; set; } = "";
    public string TaskId { get; set; } = "";
    public string Key { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string Answer { get; set; } = "";
    public bool HintsUsed { get; set; }
    public bool SolutionViewedBefore { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
