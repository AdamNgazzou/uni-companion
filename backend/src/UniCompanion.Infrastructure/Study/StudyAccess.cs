using Microsoft.EntityFrameworkCore;
using UniCompanion.Infrastructure.Persistence;

namespace UniCompanion.Infrastructure.Study;

public sealed class StudyAccess(AppDbContext db, TimeProvider clock)
{
    public async Task<bool> CanReadAsync(Guid courseId, string owner, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == owner && u.IsActive && u.EmailConfirmed &&
            (u.LockoutEnd == null || u.LockoutEnd <= clock.GetUtcNow()), ct)) return false;
        return await db.CourseMemberships.AnyAsync(m => m.CourseId == courseId && m.UserId == owner, ct) ||
            await (from ur in db.UserRoles
                   join role in db.Roles on ur.RoleId equals role.Id
                   where ur.UserId == owner && role.Name == "Admin"
                   select ur.UserId).AnyAsync(ct);
    }
}
