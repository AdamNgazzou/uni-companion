using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniCompanion.Core.Courses;
using UniCompanion.Infrastructure.Persistence;
using UniCompanion.Infrastructure.Storage;

namespace UniCompanion.Infrastructure.Courses;

public sealed class CatalogueImporter(AppDbContext db, PrivateFileStore store)
{
    public async Task<(int Courses, int Materials)> ImportAsync(string cataloguePath, CancellationToken ct = default)
    {
        var fullPath = Path.GetFullPath(cataloguePath);
        var root = Path.GetDirectoryName(fullPath)!;
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(fullPath, ct));
        // One transaction plus an advisory lock makes repeated/concurrent snapshot imports safe.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(427815916)", ct);
        var courseCount = 0;
        foreach (var item in json.RootElement.GetProperty("courses").EnumerateArray())
        {
            var sourceId = item.GetProperty("id").GetString()!;
            var course = await db.Courses.SingleOrDefaultAsync(c => c.SourceId == sourceId, ct);
            if (course is null)
            {
                course = new Course { SourceId = sourceId, IsCurated = true, IsRecognized = true };
                db.Courses.Add(course);
            }
            course.Title = item.GetProperty("title").GetString()!;
            course.Description = item.GetProperty("description").GetString();
            course.SourceMetadataJson = item.GetRawText();
            courseCount++;
        }
        await db.SaveChangesAsync(ct);
        var materialCount = 0;
        foreach (var file in json.RootElement.GetProperty("files").EnumerateArray())
        {
            if (file.GetProperty("status").GetString() != "downloaded") continue;
            var sourceCourse = file.GetProperty("courseId").GetString()!;
            var course = await db.Courses.SingleAsync(c => c.SourceId == sourceCourse, ct);
            var relative = file.GetProperty("file").GetString()!;
            var path = Path.GetFullPath(Path.Combine(root, relative));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || new FileInfo(path).LinkTarget is not null)
                throw new InvalidDataException("Catalogue file escapes its root.");
            var hash = file.GetProperty("sha256").GetString()!;
            var expectedBytes = file.GetProperty("bytes").GetInt64();
            if (new FileInfo(path).Length != expectedBytes) throw new InvalidDataException("Material size mismatch.");
            await store.PutVerifiedAsync(path, hash, ct);
            // Filename differentiates multiple files attached to one component.
            var sourceId = file.GetProperty("componentId").GetString() + ":" + relative;
            var material = await db.Materials.SingleOrDefaultAsync(m => m.CourseId == course.Id && m.SourceId == sourceId, ct);
            if (material is null)
            {
                material = new Material { CourseId = course.Id, SourceId = sourceId };
                db.Materials.Add(material);
            }
            material.Title = file.GetProperty("title").GetString()!;
            material.FileName = Path.GetFileName(relative);
            var extension = Path.GetExtension(relative).ToLowerInvariant();
            material.ContentType = extension switch { ".pdf" => "application/pdf", ".csv" => "text/csv", _ => "application/octet-stream" };
            material.SortOrder = materialCount++;
            var source = json.RootElement.GetProperty("courses").EnumerateArray().Single(c => c.GetProperty("id").GetString() == sourceCourse);
            material.LessonId = source.GetProperty("chapters").EnumerateArray().FirstOrDefault(ch =>
                ch.GetProperty("components").EnumerateArray().Any(component => component.GetProperty("id").GetString() == file.GetProperty("componentId").GetString()))
                .GetProperty("id").GetString();
            await db.SaveChangesAsync(ct);
            var version = await db.MaterialVersions.SingleOrDefaultAsync(v => v.MaterialId == material.Id && v.Sha256 == hash, ct);
            if (version is null)
            {
                version = new MaterialVersion
                {
                    MaterialId = material.Id,
                    Sha256 = hash,
                    Bytes = expectedBytes,
                    PageCount = file.TryGetProperty("pages", out var pages) ? pages.GetInt32() : null
                };
                db.MaterialVersions.Add(version);
                await db.SaveChangesAsync(ct);
            }
            material.CurrentVersionId = version.Id;
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (courseCount, materialCount);
    }
}
