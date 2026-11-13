using Microsoft.EntityFrameworkCore;
using UniCompanion.Core.Activity;
using UniCompanion.Infrastructure.Persistence;

namespace UniCompanion.Infrastructure.Activity;

public static class ActivityVisibility
{
    public static IQueryable<ActivityItem> Query(AppDbContext db, string owner, bool admin) => db.ActivityItems.AsNoTracking().Where(item => item.OwnerId == owner &&
        ((item.Kind == "revision_reminder") || ((item.Kind == "study_completed" || item.Kind == "study_failed") && db.StudyJobs.Any(j => j.Id == item.SourceId && j.OwnerId == owner && (admin || db.CourseMemberships.Any(m => m.CourseId == j.CourseId && m.UserId == owner)))) ||
        ((item.Kind == "reply" || item.Kind == "upvote") && db.CommunityPosts.Any(p => p.Id == item.SourceId && !p.IsDeleted && !p.IsSample &&
            !db.CommunityBlocks.Any(b => b.OwnerId == owner && b.BlockedUserId == p.AuthorId && b.IsAnonymous == p.IsAnonymous) &&
            (p.ParentId == null || db.CommunityPosts.Any(root => root.Id == p.ParentId && !root.IsDeleted && !db.CommunityBlocks.Any(b => b.OwnerId == owner && b.BlockedUserId == root.AuthorId && b.IsAnonymous == root.IsAnonymous)))))));
}
