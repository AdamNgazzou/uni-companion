namespace UniCompanion.Core.Learning;

public sealed class RevisionEntry
{
    public string OwnerId { get; set; } = "";
    public string Key { get; set; } = "";
    public string CreationHash { get; set; } = "";
    public string Title { get; set; } = "";
    public string Topic { get; set; } = "";
    public string Note { get; set; } = "";
    public string State { get; set; } = "active";
    public DateTimeOffset? SnoozedUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class StudyReminder
{
    public string OwnerId { get; set; } = "";
    public Guid Id { get; set; } = Guid.NewGuid();
    public bool Enabled { get; set; }
    public int MinuteOfDay { get; set; } = 18 * 60;
    public string TimeZoneId { get; set; } = "Africa/Lagos";
    public int QuietStart { get; set; } = 22 * 60;
    public int QuietEnd { get; set; } = 7 * 60;
    public DateTimeOffset? NextDueAt { get; set; }
}
