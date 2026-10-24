using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniCompanion.Core.Practice;
using UniCompanion.Core.Study;
using UniCompanion.Core.Tutor;
using UniCompanion.Infrastructure.Persistence;
using UniCompanion.Infrastructure.Study;

namespace UniCompanion.Infrastructure.Tutor;

public sealed class TutorConversations(AppDbContext db, StudyAccess access, StudyJobs jobs, TimeProvider clock)
{
    private Task LockAsync(string owner, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"tutor:" + owner}, 0))", ct);
    public async Task<TutorConversation> GetAsync(Guid id, string owner, CancellationToken ct)
    {
        var conversation = await db.TutorConversations.SingleOrDefaultAsync(c => c.Id == id && c.OwnerId == owner, ct);
        if (conversation is null || !await access.CanReadAsync(conversation.CourseId, owner, ct)) throw new StudyRequestException(404, "not_found");
        return conversation;
    }
    private async Task ValidateScope(Guid course, string owner, ConversationRequest request, CancellationToken ct)
    {
        if (request.Language is not ("en" or "fr") || request.ChapterId?.Length > 100) throw new StudyRequestException(400, "invalid_conversation");
        if (!await db.Courses.AnyAsync(c => c.Id == course, ct) || !await access.CanReadAsync(course, owner, ct)) throw new StudyRequestException(404, "not_found");
        if (request.MaterialId is not null || request.ChapterId is { Length: > 0 })
        {
            var material = db.Materials.Where(m => m.CourseId == course && m.ContentType == "application/pdf" && m.CurrentVersionId != null);
            if (request.MaterialId is { } id) material = material.Where(m => m.Id == id);
            if (request.ChapterId is { Length: > 0 } chapter) material = material.Where(m => m.LessonId == chapter);
            if (!await material.AnyAsync(ct)) throw new StudyRequestException(404, "not_found");
        }
    }
    private async Task<TutorConversation> CreateInsideAsync(Guid course, string owner, string key, ConversationRequest request, Guid? attempt, string? title, CancellationToken ct)
    {
        StudyJobs.ValidateKey(key); await ValidateScope(course, owner, request, ct);
        var hash = StudyJobs.Hash(JsonSerializer.Serialize(new { course, request, attempt }, StudyJobs.Json));
        var existing = await db.TutorConversations.SingleOrDefaultAsync(c => c.OwnerId == owner && c.RequestKey == key, ct);
        if (existing is not null)
        {
            if (existing.RequestHash != hash) throw new StudyRequestException(409, "idempotency_conflict");
            return existing;
        }
        if (attempt is { } attemptId)
        {
            existing = await db.TutorConversations.SingleOrDefaultAsync(c => c.OwnerId == owner && c.PracticeAttemptId == attemptId, ct);
            if (existing is not null) return existing;
        }
        if (await db.TutorConversations.CountAsync(c => c.OwnerId == owner, ct) >= 100) throw new StudyRequestException(409, "conversation_limit");
        var conversation = new TutorConversation
        {
            OwnerId = owner,
            CourseId = course,
            MaterialId = request.MaterialId,
            ChapterId = request.ChapterId,
            Language = request.Language,
            PracticeAttemptId = attempt,
            Title = title ?? "New conversation",
            RequestKey = key,
            RequestHash = hash,
            CreatedAt = clock.GetUtcNow(),
            UpdatedAt = clock.GetUtcNow()
        };
        db.TutorConversations.Add(conversation); await db.SaveChangesAsync(ct); return conversation;
    }
    public async Task<TutorConversation> CreateAsync(Guid course, string owner, string key, ConversationRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(owner, ct);
        var conversation = await CreateInsideAsync(course, owner, key, request, null, null, ct);
        await tx.CommitAsync(ct); return conversation;
    }
    public async Task<TutorTurn> SendAsync(Guid id, string owner, string key, TurnRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(owner, ct);
        var conversation = await GetAsync(id, owner, ct);
        var turn = await SendInsideAsync(conversation, owner, key, request, ct);
        await tx.CommitAsync(ct); return turn;
    }
    private async Task<(TutorFeedback Feedback, Guid[] Versions)> FeedbackAsync(TutorConversation conversation, string owner, CancellationToken ct)
    {
        var attempt = await db.PracticeAttempts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == conversation.PracticeAttemptId && a.OwnerId == owner, ct);
        if (attempt is null) throw new StudyRequestException(404, "not_found");
        var job = await jobs.GetAsync(attempt.JobId, owner, ct);
        if (job.Kind != "practice" || job.State != JobStates.Succeeded || job.ResultJson is null || !await db.PracticeProgress.AnyAsync(p => p.JobId == job.Id && p.OwnerId == owner && p.TaskId == attempt.TaskId && p.RevealedAt != null, ct))
            throw new StudyRequestException(409, "practice_not_ready");
        var task = JsonSerializer.Deserialize<StudyResult>(job.ResultJson, StudyJobs.Json)!.Practice!.Tasks.Single(t => t.Id == attempt.TaskId);
        var feedback = new TutorFeedback(task.Prompt, task.Given, attempt.Answer, task.Rubric, task.Solution, task.SolutionCode);
        if (JsonSerializer.Serialize(feedback, StudyJobs.Json).Length > 20000) throw new StudyRequestException(400, "feedback_scope_too_large");
        return (feedback, job.SourceVersionIds);
    }
    private async Task<TutorTurn> SendInsideAsync(TutorConversation conversation, string owner, string key, TurnRequest request, CancellationToken ct)
    {
        StudyJobs.ValidateKey(key); var question = request.Question?.Trim();
        if (question is null || question.Length is < 1 or > 2000 || request.Action is not ("explain" or "hint" or "check")) throw new StudyRequestException(400, "invalid_tutor_message");
        var hash = StudyJobs.Hash(JsonSerializer.Serialize(new { conversation.Id, question, request.Action }, StudyJobs.Json));
        var replay = await db.TutorTurns.SingleOrDefaultAsync(t => t.OwnerId == owner && t.RequestKey == key, ct);
        if (replay is not null)
        {
            if (replay.RequestHash != hash) throw new StudyRequestException(409, "idempotency_conflict");
            return replay;
        }
        var turns = await (from t in db.TutorTurns where t.ConversationId == conversation.Id && t.OwnerId == owner join j in db.StudyJobs on t.JobId equals j.Id orderby t.Ordinal select new { Turn = t, Job = j }).ToArrayAsync(ct);
        if (turns.Any(t => JobStates.IsPending(t.Job.State))) throw new StudyRequestException(409, "conversation_busy");
        if (turns.Length >= 40) throw new StudyRequestException(409, "conversation_turn_limit");
        var history = turns.Where(t => t.Job.State == JobStates.Succeeded && t.Job.ResultJson != null).TakeLast(3).Select(t =>
        {
            var result = JsonSerializer.Deserialize<StudyResult>(t.Job.ResultJson!, StudyJobs.Json)!;
            var answer = string.Join("\n", result.Paragraphs.Select(p => p.Text));
            return new TutorHistory(t.Turn.Question[..Math.Min(t.Turn.Question.Length, 1000)], answer[..Math.Min(answer.Length, 1800)]);
        }).ToArray();
        TutorFeedback? feedback = null; Guid[]? versions = null;
        if (conversation.PracticeAttemptId is not null)
        {
            var context = await FeedbackAsync(conversation, owner, ct); feedback = context.Feedback; versions = context.Versions;
            history = history.TakeLast(2).ToArray();
        }
        var ordinal = turns.Length + 1;
        var input = new StudyRequest("tutor", question, conversation.MaterialId, conversation.ChapterId, conversation.Language,
            Tutor: new TutorContext(conversation.Id, ordinal, request.Action, history, feedback));
        var job = await jobs.EnqueueInTransactionAsync(conversation.CourseId, owner, StudyJobs.Hash("tutor:" + key), input, ct, true, versions);
        var turn = new TutorTurn
        {
            ConversationId = conversation.Id,
            OwnerId = owner,
            Ordinal = ordinal,
            RequestKey = key,
            RequestHash = hash,
            Question = question,
            Action = request.Action,
            JobId = job.Id,
            CreatedAt = clock.GetUtcNow()
        };
        db.TutorTurns.Add(turn); conversation.UpdatedAt = clock.GetUtcNow();
        if (ordinal == 1 && conversation.PracticeAttemptId is null) conversation.Title = question[..Math.Min(question.Length, 120)];
        await db.SaveChangesAsync(ct); return turn;
    }
    public async Task<TutorConversation> StartFeedbackAsync(Guid jobId, Guid attemptId, string owner, string key, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(owner, ct);
        var attempt = await db.PracticeAttempts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == attemptId && a.JobId == jobId && a.OwnerId == owner, ct);
        if (attempt is null) throw new StudyRequestException(404, "not_found");
        var job = await jobs.GetAsync(jobId, owner, ct);
        if (job.Kind != "practice" || job.State != JobStates.Succeeded || job.ResultJson is null) throw new StudyRequestException(409, "practice_not_ready");
        var request = JsonSerializer.Deserialize<StudyRequest>(job.RequestJson, StudyJobs.Json)!;
        var task = JsonSerializer.Deserialize<StudyResult>(job.ResultJson, StudyJobs.Json)!.Practice!.Tasks.Single(t => t.Id == attempt.TaskId);
        var title = "Feedback: " + task.Title; title = title[..Math.Min(title.Length, 160)];
        var conversation = await CreateInsideAsync(job.CourseId, owner, key, new ConversationRequest(request.MaterialId, request.ChapterId, request.Language), attemptId, title, ct);
        if (!await db.TutorTurns.AnyAsync(t => t.ConversationId == conversation.Id, ct))
            await SendInsideAsync(conversation, owner, StudyJobs.Hash("feedback-turn:" + key), new TurnRequest(request.Language == "fr" ? "Relis ma tentative enregistrée et propose une prochaine étape." : "Review my saved attempt and suggest one next step.", "check"), ct);
        await tx.CommitAsync(ct); return conversation;
    }
    public async Task DeleteAsync(Guid? id, string owner, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(owner, ct);
        if (id is { } selected) await GetAsync(selected, owner, ct);
        var conversations = db.TutorConversations.Where(c => c.OwnerId == owner && (id == null || c.Id == id));
        var conversationIds = await conversations.Select(c => c.Id).ToArrayAsync(ct);
        var jobIds = await db.TutorTurns.Where(t => t.OwnerId == owner && conversationIds.Contains(t.ConversationId)).Select(t => t.JobId).ToArrayAsync(ct);
        await db.StudyJobs.Where(j => j.OwnerId == owner && jobIds.Contains(j.Id)).ExecuteDeleteAsync(ct);
        await conversations.ExecuteDeleteAsync(ct); await tx.CommitAsync(ct);
    }
}
