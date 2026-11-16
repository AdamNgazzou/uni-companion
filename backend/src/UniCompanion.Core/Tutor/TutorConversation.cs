namespace UniCompanion.Core.Tutor;

public sealed class TutorConversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OwnerId { get; set; } = "";
    public Guid CourseId { get; set; }
    public Guid? MaterialId { get; set; }
    public string? ChapterId { get; set; }
    public Guid? PracticeAttemptId { get; set; }
    public string Language { get; set; } = "en";
    public string Title { get; set; } = "New conversation";
    public string RequestKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class TutorTurn
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public string OwnerId { get; set; } = "";
    public int Ordinal { get; set; }
    public string RequestKey { get; set; } = "";
    public string RequestHash { get; set; } = "";
    public string Question { get; set; } = "";
    public string Action { get; set; } = "explain";
    public Guid JobId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed record TutorHistory(string Question, string AnswerExcerpt);
public sealed record TutorFeedback(string Task, string Given, string Answer, string[] Rubric, string Solution, string? SolutionCode);
// Internal generation input. Never accept this context from a public job request or return it in a job DTO.
public sealed record TutorContext(Guid ConversationId, int Ordinal, string Action, TutorHistory[] History, TutorFeedback? Feedback = null);
public sealed record ConversationRequest(Guid? MaterialId = null, string? ChapterId = null, string Language = "en");
public sealed record TurnRequest(string? Question, string Action = "explain");
