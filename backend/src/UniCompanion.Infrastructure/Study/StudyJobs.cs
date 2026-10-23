using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniCompanion.Core.Study;
using UniCompanion.Infrastructure.Ai;
using UniCompanion.Infrastructure.Persistence;

namespace UniCompanion.Infrastructure.Study;

public sealed class StudyJobs(AppDbContext db, StudyAccess access, TimeProvider clock)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public const string PromptVersion = "study-v3-study-notes-gemini-protocol1";
    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static bool Pending(string state) => JobStates.IsPending(state);
    public static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 100 || key.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_' or ':')))
            throw new StudyRequestException(400, "invalid_idempotency_key");
    }

    public async Task<StudyJob> EnqueueAsync(Guid courseId, string owner, string key, StudyRequest request, CancellationToken ct)
    {
        ValidateKey(key);
        if (request.Kind is not ("answer" or "summary" or "quiz") || request.Language is not ("en" or "fr") ||
            (request.Kind == "answer" && (string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > 2000)) ||
            (request.Kind != "answer" && request.MaterialId is null && string.IsNullOrWhiteSpace(request.ChapterId)) ||
            (request.Kind == "quiz" && request.Count is not (5 or 10)) || request.ChapterId?.Length > 100)
            throw new StudyRequestException(400, "invalid_study_request");
        if (!await access.CanReadAsync(courseId, owner, ct)) throw new StudyRequestException(404, "not_found");
        var requestJson = JsonSerializer.Serialize(request, Json);
        var requestHash = Hash(courseId + requestJson);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // One admission lock enforces both account quotas and the small global queue ceiling.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(427815918)", ct);
        var mapping = await db.StudyRequestKeys.SingleOrDefaultAsync(x => x.OwnerId == owner && x.Key == key, ct);
        if (mapping is not null)
        {
            if (mapping.RequestHash != requestHash) throw new StudyRequestException(409, "idempotency_conflict");
            return await db.StudyJobs.SingleAsync(x => x.Id == mapping.JobId && x.OwnerId == owner, ct);
        }
        var materials = db.Materials.AsNoTracking().Where(m => m.CourseId == courseId && m.ContentType == "application/pdf" && m.CurrentVersionId != null);
        if (request.MaterialId is { } materialId) materials = materials.Where(m => m.Id == materialId);
        if (request.ChapterId is { Length: > 0 } chapter) materials = materials.Where(m => m.LessonId == chapter);
        var versions = await materials.OrderBy(m => m.SortOrder).ThenBy(m => m.Id).Select(m => m.CurrentVersionId!.Value).ToArrayAsync(ct);
        if ((request.MaterialId is not null || request.ChapterId is not null) && versions.Length == 0)
            throw new StudyRequestException(404, "not_found");
        if (versions.Length > 0 && request.Kind != "answer")
        {
            var indexed = await db.DocumentChunks.Where(c => versions.Contains(c.MaterialVersionId) && c.Model == PythonAiBridge.Model)
                .Select(c => c.MaterialVersionId).Distinct().CountAsync(ct);
            if (indexed != versions.Length) throw new StudyRequestException(409, "material_processing");
            var chunkCount = await db.DocumentChunks.CountAsync(c => versions.Contains(c.MaterialVersionId) && c.Model == PythonAiBridge.Model, ct);
            if (chunkCount > 140) throw new StudyRequestException(400, "scope_too_large");
        }
        var fingerprint = Hash(requestHash + string.Join(',', versions.Order()) + PromptVersion + PythonAiBridge.Model);
        var cached = await db.StudyJobs.FirstOrDefaultAsync(x => x.OwnerId == owner && x.CourseId == courseId && x.Fingerprint == fingerprint && x.State == JobStates.Succeeded, ct);
        if (cached is not null)
        {
            db.StudyRequestKeys.Add(new StudyRequestKey { OwnerId = owner, Key = key, RequestHash = requestHash, JobId = cached.Id });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return cached;
        }
        var now = clock.GetUtcNow();
        if (await db.StudyJobs.CountAsync(x => x.OwnerId == owner && x.CreatedAt >= now.AddMinutes(-1), ct) >= 5 ||
            await db.StudyJobs.CountAsync(x => x.OwnerId == owner && x.CreatedAt >= now.AddHours(-24), ct) >= 50 ||
            await db.StudyJobs.CountAsync(x => x.OwnerId == owner && (x.State == JobStates.Queued || x.State == JobStates.Running || x.State == JobStates.Retry), ct) >= 2 ||
            await db.StudyJobs.CountAsync(x => x.State == JobStates.Queued || x.State == JobStates.Running || x.State == JobStates.Retry, ct) >= 20)
            throw new StudyRequestException(429, "study_limit");
        var job = new StudyJob
        {
            OwnerId = owner,
            CourseId = courseId,
            Kind = request.Kind,
            RequestJson = requestJson,
            SourceVersionIds = versions,
            Fingerprint = fingerprint,
            CreatedAt = now,
            UpdatedAt = now,
            NextAttemptAt = now
        };
        db.StudyJobs.Add(job);
        db.StudyRequestKeys.Add(new StudyRequestKey { OwnerId = owner, Key = key, RequestHash = requestHash, JobId = job.Id });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return job;
    }

    public async Task<StudyJob> GetAsync(Guid id, string owner, CancellationToken ct)
    {
        var job = await db.StudyJobs.AsNoTracking().SingleOrDefaultAsync(j => j.Id == id && j.OwnerId == owner, ct);
        if (job is null || !await access.CanReadAsync(job.CourseId, owner, ct)) throw new StudyRequestException(404, "not_found");
        return job;
    }

    public async Task<bool> IsStaleAsync(StudyJob job, CancellationToken ct)
    {
        var request = JsonSerializer.Deserialize<StudyRequest>(job.RequestJson, Json)!;
        var materials = db.Materials.Where(m => m.CourseId == job.CourseId && m.ContentType == "application/pdf" && m.CurrentVersionId != null);
        if (request.MaterialId is { } id) materials = materials.Where(m => m.Id == id);
        if (request.ChapterId is { Length: > 0 } chapter) materials = materials.Where(m => m.LessonId == chapter);
        var current = await materials.Select(m => m.CurrentVersionId!.Value).ToArrayAsync(ct);
        return !current.Order().SequenceEqual(job.SourceVersionIds.Order());
    }

    public async Task<Dictionary<Guid, StudyLabels>> DescribeAsync(IEnumerable<StudyJob> jobs, CancellationToken ct)
    {
        var rows = jobs.ToArray();
        var requests = rows.ToDictionary(j => j.Id, j => JsonSerializer.Deserialize<StudyRequest>(j.RequestJson, Json)!);
        var courses = rows.Select(j => j.CourseId).Distinct().ToArray();
        var materials = requests.Values.Where(r => r.MaterialId != null).Select(r => r.MaterialId!.Value).Distinct().ToArray();
        var courseNames = await db.Courses.Where(c => courses.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Title, ct);
        var materialNames = await db.Materials.Where(m => materials.Contains(m.Id)).ToDictionaryAsync(m => m.Id, m => m.Title, ct);
        return rows.ToDictionary(j => j.Id, j =>
        {
            var request = requests[j.Id];
            var course = courseNames.GetValueOrDefault(j.CourseId, "Course");
            var scope = request.MaterialId is { } id ? materialNames.GetValueOrDefault(id, "Course material") : request.ChapterId is not null ? "Chapter materials" : course;
            return new StudyLabels(request.Kind == "answer" ? request.Question! : scope, course, scope);
        });
    }

    public static object PublicResult(StudyResult result) => result.Kind == "quiz"
        ? new { result.Kind, result.InsufficientEvidence, questions = result.Questions.Select(q => new { q.Id, q.Question, q.Options, q.Citations }), result.CoveredPages, result.TotalPages }
        : new { result.Kind, result.InsufficientEvidence, result.Paragraphs, result.CoveredPages, result.TotalPages };

    public async Task<object> SubmitAsync(Guid jobId, string owner, string key, AttemptRequest request, CancellationToken ct)
    {
        ValidateKey(key);
        var job = await GetAsync(jobId, owner, ct);
        if (job.Kind != "quiz" || job.State != JobStates.Succeeded || job.ResultJson is null) throw new StudyRequestException(409, "quiz_not_ready");
        var quiz = JsonSerializer.Deserialize<StudyResult>(job.ResultJson, Json)!;
        if (request.Answers is null || request.Answers.Any(a => a is null) || request.Answers.Length != quiz.Questions.Length || request.Answers.Select(a => a.QuestionId).Distinct().Count() != quiz.Questions.Length ||
            request.Answers.Any(a => !quiz.Questions.Any(q => q.Id == a.QuestionId && q.Options.Any(o => o.Id == a.OptionId))))
            throw new StudyRequestException(400, "invalid_answers");
        var canonical = JsonSerializer.Serialize(request.Answers.OrderBy(a => a.QuestionId), Json);
        var hash = Hash(jobId + canonical);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(427815919)", ct);
        var existing = await db.QuizAttempts.SingleOrDefaultAsync(a => a.OwnerId == owner && a.Key == key, ct);
        if (existing is not null)
        {
            if (existing.RequestHash != hash) throw new StudyRequestException(409, "idempotency_conflict");
            return JsonSerializer.Deserialize<JsonElement>(existing.ResultJson);
        }
        var id = Guid.NewGuid();
        var now = clock.GetUtcNow();
        var corrections = quiz.Questions.Select(q => new
        {
            questionId = q.Id,
            q.Question,
            q.Options,
            selectedOptionId = request.Answers.Single(a => a.QuestionId == q.Id).OptionId,
            q.CorrectOptionId,
            q.Explanation,
            q.Citations
        }).ToArray();
        var result = new
        {
            id,
            jobId,
            courseId = job.CourseId,
            createdAt = now,
            correct = corrections.Count(c => c.selectedOptionId == c.CorrectOptionId),
            total = corrections.Length,
            corrections
        };
        var resultJson = JsonSerializer.Serialize(result, Json);
        db.QuizAttempts.Add(new QuizAttempt { Id = id, JobId = jobId, OwnerId = owner, Key = key, RequestHash = hash, CreatedAt = now, ResultJson = resultJson });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}
