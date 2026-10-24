using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniCompanion.Core.Learning;
using UniCompanion.Core.Study;
using UniCompanion.Infrastructure.Persistence;
using UniCompanion.Infrastructure.Study;

namespace UniCompanion.Infrastructure.Learning;

public sealed record RevisionCard(string Key, string Kind, string Title, string Topic, string Note, string State, DateTimeOffset? SnoozedUntil, DateTimeOffset CreatedAt, string? CourseTitle = null, Guid? AttemptId = null, string? QuestionId = null, string? OriginalQuestion = null);
public sealed record ConceptRequest(string Title, string Topic = "", string Note = "");
public sealed record RevisionUpdate(string Title, string Topic, string Note, string State = "active", int? SnoozeDays = null);

public sealed class LearningProgress(AppDbContext db, TimeProvider clock)
{
    public async Task<IQueryable<StudyJob>> JobsAsync(string owner, CancellationToken ct)
    {
        var admin = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id where ur.UserId == owner && r.Name == "Admin" select r.Id).AnyAsync(ct);
        return db.StudyJobs.AsNoTracking().Where(j => j.OwnerId == owner && (admin || db.CourseMemberships.Any(m => m.CourseId == j.CourseId && m.UserId == owner)));
    }
    public async Task<object> CountsAsync(string owner, CancellationToken ct)
    {
        var jobs = await JobsAsync(owner, ct);
        var attempts = await (from a in db.PracticeAttempts.AsNoTracking() join j in jobs on a.JobId equals j.Id where a.OwnerId == owner select new { a.JobId, a.TaskId, a.HintsUsed, a.SolutionViewedBefore }).ToArrayAsync(ct);
        var ids = attempts.Select(a => a.JobId).Distinct().ToArray();
        var practice = await jobs.Where(j => ids.Contains(j.Id) && j.ResultJson != null).ToDictionaryAsync(j => j.Id, j => j.ResultJson!, ct);
        var kinds = practice.ToDictionary(p => p.Key, p => JsonSerializer.Deserialize<StudyResult>(p.Value, StudyJobs.Json)!.Practice!.Tasks.ToDictionary(t => t.Id, t => t.Kind));
        var tasks = attempts.DistinctBy(a => (a.JobId, a.TaskId)).ToArray();
        var quiz = from a in db.QuizAttempts.AsNoTracking() join j in jobs on a.JobId equals j.Id where a.OwnerId == owner select a;
        return new
        {
            savedSessions = await jobs.CountAsync(j => j.Kind != "tutor" && j.State == JobStates.Succeeded, ct),
            quizAttempts = await quiz.CountAsync(ct),
            quizzesAttempted = await quiz.Select(a => a.JobId).Distinct().CountAsync(ct),
            writtenTasksAttempted = tasks.Count(a => kinds[a.JobId].GetValueOrDefault(a.TaskId) == "written"),
            codingLabsAttempted = tasks.Count(a => kinds[a.JobId].GetValueOrDefault(a.TaskId) == "coding"),
            practiceAttempts = attempts.Length,
            assistedTasks = attempts.Where(a => a.HintsUsed || a.SolutionViewedBefore).DistinctBy(a => (a.JobId, a.TaskId)).Count(),
            selfCompletedTasks = await (from p in db.PracticeProgress join j in jobs on p.JobId equals j.Id where p.OwnerId == owner && p.CompletedAt != null select p).CountAsync(ct),
            feedbackConversations = await db.TutorConversations.CountAsync(c => c.OwnerId == owner && c.PracticeAttemptId != null && jobs.Any(j => j.CourseId == c.CourseId && db.TutorTurns.Any(t => t.ConversationId == c.Id && t.JobId == j.Id && j.State == JobStates.Succeeded)), ct)
        };
    }
    public async Task<RevisionCard[]> CardsAsync(string owner, CancellationToken ct)
    {
        var jobs = await JobsAsync(owner, ct);
        var attempts = await (from a in db.QuizAttempts.AsNoTracking() join j in jobs on a.JobId equals j.Id where a.OwnerId == owner select a)
            .GroupBy(a => a.JobId).Select(g => g.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id).First()).ToArrayAsync(ct);
        var courseNames = await (from j in jobs join c in db.Courses on j.CourseId equals c.Id select new { j.Id, c.Title }).ToDictionaryAsync(x => x.Id, x => x.Title, ct);
        var entries = await db.RevisionEntries.AsNoTracking().Where(e => e.OwnerId == owner).ToDictionaryAsync(e => e.Key, ct);
        var cards = new List<RevisionCard>();
        foreach (var attempt in attempts)
        {
            using var result = JsonDocument.Parse(attempt.ResultJson);
            foreach (var q in result.RootElement.GetProperty("corrections").EnumerateArray())
            {
                if (q.GetProperty("selectedOptionId").GetString() == q.GetProperty("correctOptionId").GetString()) continue;
                var questionId = q.GetProperty("questionId").GetString()!; var question = q.GetProperty("question").GetString()!;
                var key = "quiz:" + attempt.JobId + ":" + questionId;
                cards.Add(Apply(new RevisionCard(key, "quiz", question[..Math.Min(question.Length, 160)], "", "", "active", null, attempt.CreatedAt, courseNames[attempt.JobId], attempt.Id, questionId, question), entries.GetValueOrDefault(key)));
            }
        }
        foreach (var entry in entries.Values.Where(e => e.Key.StartsWith("concept:", StringComparison.Ordinal)))
            cards.Add(Apply(new RevisionCard(entry.Key, "concept", entry.Title, entry.Topic, entry.Note, entry.State, entry.SnoozedUntil, entry.CreatedAt), entry));
        return cards.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Key, StringComparer.Ordinal).ToArray();
    }
    private RevisionCard Apply(RevisionCard card, RevisionEntry? entry) => entry is null ? card : card with
    {
        Title = entry.Title,
        Topic = entry.Topic,
        Note = entry.Note,
        State = entry.State == "snoozed" && entry.SnoozedUntil <= clock.GetUtcNow() ? "active" : entry.State,
        SnoozedUntil = entry.SnoozedUntil
    };
    private static void Validate(string title, string topic, string note)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 160 || topic is null || note is null || topic.Length > 80 || note.Length > 2000) throw new StudyRequestException(400, "invalid_revision_entry");
    }
    private Task LockAsync(string owner, CancellationToken ct) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"learning:" + owner},0))", ct);
    public async Task<string> CreateAsync(string owner, string key, ConceptRequest request, CancellationToken ct)
    {
        StudyJobs.ValidateKey(key); Validate(request.Title, request.Topic, request.Note);
        var source = "concept:" + StudyJobs.Hash(key)[..32]; var hash = StudyJobs.Hash(JsonSerializer.Serialize(request, StudyJobs.Json));
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(owner, ct);
        var existing = await db.RevisionEntries.FindAsync([owner, source], ct);
        if (existing is not null) { if (existing.CreationHash != hash) throw new StudyRequestException(409, "idempotency_conflict"); return source; }
        if (await db.RevisionEntries.CountAsync(e => e.OwnerId == owner && e.Key.StartsWith("concept:"), ct) >= 500) throw new StudyRequestException(409, "revision_limit");
        db.RevisionEntries.Add(new RevisionEntry { OwnerId = owner, Key = source, CreationHash = hash, Title = request.Title.Trim(), Topic = request.Topic.Trim(), Note = request.Note.Trim(), CreatedAt = clock.GetUtcNow() });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return source;
    }
    public async Task UpdateAsync(string owner, string key, RevisionUpdate request, CancellationToken ct)
    {
        Validate(request.Title, request.Topic, request.Note);
        if (request.State is not ("active" or "dismissed" or "snoozed") || request.SnoozeDays is not (null or 1 or 3 or 7)) throw new StudyRequestException(400, "invalid_revision_entry");
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockAsync(owner, ct);
        var card = (await CardsAsync(owner, ct)).SingleOrDefault(c => c.Key == key) ?? throw new StudyRequestException(404, "not_found");
        if (request.State == "snoozed" && request.SnoozeDays is null && card.State != "snoozed") throw new StudyRequestException(400, "invalid_revision_entry");
        var entry = await db.RevisionEntries.FindAsync([owner, key], ct);
        if (entry is null) { entry = new RevisionEntry { OwnerId = owner, Key = key, CreatedAt = card.CreatedAt }; db.RevisionEntries.Add(entry); }
        entry.Title = request.Title.Trim(); entry.Topic = request.Topic.Trim(); entry.Note = request.Note.Trim(); entry.State = request.State;
        entry.SnoozedUntil = request.State == "snoozed" ? request.SnoozeDays is { } days ? clock.GetUtcNow().AddDays(days) : card.SnoozedUntil : null;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
