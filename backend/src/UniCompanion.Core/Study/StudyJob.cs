using System.Text.Json.Serialization;
using UniCompanion.Core.Practice;

namespace UniCompanion.Core.Study;

public static class JobStates
{
    public const string Queued = "queued", Running = "running", Retry = "retry_wait", Succeeded = "succeeded", Failed = "failed", Cancelled = "cancelled";
    public static bool IsPending(string state) => state is Queued or Running or Retry;
}

public sealed class StudyJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = "";
    public Guid CourseId { get; set; }
    public string Kind { get; set; } = "";
    public string RequestJson { get; set; } = "{}";
    public Guid[] SourceVersionIds { get; set; } = [];
    public string Fingerprint { get; set; } = "";
    public string State { get; set; } = JobStates.Queued;
    public string Stage { get; set; } = "Waiting for your study session";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public Guid? LeaseOwner { get; set; }
    public long Fence { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public int Attempts { get; set; }
    public int ProviderCalls { get; set; }
    public string? ErrorCode { get; set; }
    public string? ResultJson { get; set; }
}

public sealed class StudyRequestKey
{
    public string OwnerId { get; set; } = "";
    public string Key { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public Guid JobId { get; set; }
}

public sealed class StudyCheckpoint
{
    public Guid JobId { get; set; }
    public string Key { get; set; } = "";
    public string ResultJson { get; set; } = "{}";
}

public sealed class QuizAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public string OwnerId { get; set; } = "";
    public string Key { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string ResultJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed record StudyRequest(string Kind, string? Question = null, Guid? MaterialId = null, string? ChapterId = null, string Language = "en", int Count = 5, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PracticeOptions? Practice = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] UniCompanion.Core.Tutor.TutorContext? Tutor = null);
public sealed record StudySource(Guid ChunkId, Guid MaterialId, Guid VersionId, string FileName, int Page, string Text);
public sealed record StudyCitation(Guid ChunkId, Guid MaterialId, Guid VersionId, string FileName, int Page, string Quote);
public sealed record StudyParagraph(string Text, StudyCitation[] Citations, string? Heading = null, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Basis = null);
public sealed record StudyOption(string Id, string Text);
public sealed record StudyQuestion(string Id, string Question, StudyOption[] Options, string CorrectOptionId, string Explanation, StudyCitation[] Citations);
public sealed record StudyResult(string Kind, bool InsufficientEvidence, StudyParagraph[] Paragraphs, StudyQuestion[] Questions, int CoveredPages = 0, int TotalPages = 0, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] PracticeArtifact? Practice = null);
public sealed record AnswerSubmission(string QuestionId, string OptionId);
public sealed record AttemptRequest(AnswerSubmission[] Answers);

public sealed class StudyRequestException(int status, string code) : Exception(code)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
public sealed class GenerationFailure(string code, bool retryable, int? retryAfter = null) : Exception(code)
{
    public string Code { get; } = code;
    public bool Retryable { get; } = retryable;
    public int? RetryAfter { get; } = retryAfter;
}
public interface ITextGeneration
{
    Task<string> GenerateAsync(string prompt, CancellationToken cancellationToken);
}

public sealed record StudyLabels(string Title, string CourseTitle, string ScopeTitle);
