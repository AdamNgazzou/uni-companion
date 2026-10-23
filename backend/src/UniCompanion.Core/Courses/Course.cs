namespace UniCompanion.Core.Courses;

public sealed class Course
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? CreatorId { get; set; }
    public bool IsCurated { get; set; }
    public bool IsRecognized { get; set; }
    public string? SourceId { get; set; }
    public string? SourceMetadataJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CourseMembership
{
    public Guid CourseId { get; set; }
    public string UserId { get; set; } = "";
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Material
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CourseId { get; set; }
    public string Title { get; set; } = "";
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public string SourceId { get; set; } = "";
    public string? LessonId { get; set; }
    public int SortOrder { get; set; }
    public Guid? CurrentVersionId { get; set; }
}

public sealed class MaterialVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MaterialId { get; set; }
    public string Sha256 { get; set; } = "";
    public long Bytes { get; set; }
    public int? PageCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
