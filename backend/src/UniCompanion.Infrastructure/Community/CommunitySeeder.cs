using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniCompanion.Core.Community;
using UniCompanion.Infrastructure.Identity;
using UniCompanion.Infrastructure.Persistence;

namespace UniCompanion.Infrastructure.Community;

public sealed class CommunitySeeder(AppDbContext db, UserManager<AppUser> users)
{
    private static Guid Id(string key) => new(SHA256.HashData(Encoding.UTF8.GetBytes("uni-community-demo-v1:" + key))[..16]);
    public async Task<(int Users, int Posts, int Replies, int Votes)> SeedAsync(CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(94632817)", ct);
        string[] names = ["Lina Ben Amor", "Youssef Haddad", "Maya Trabelsi", "Aziz Kacem", "Nour Saidi", "Sami Belhaj", "Ines Jaziri", "Rayan Ferchichi"];
        var ids = new List<string>();
        for (var i = 0; i < names.Length; i++)
        {
            var id = $"uni-demo-community-{i + 1:00}";
            var email = $"uni-demo-community-{i + 1:00}@example.test";
            var existing = await users.FindByIdAsync(id);
            if (existing is null)
            {
                var created = await users.CreateAsync(new AppUser
                {
                    Id = id,
                    Email = email,
                    UserName = email,
                    DisplayName = names[i],
                    EmailConfirmed = false,
                    IsActive = false
                });
                if (!created.Succeeded) throw new InvalidOperationException("Could not create a disabled demo identity: " + string.Join(",", created.Errors.Select(e => e.Code)));
            }
            else if (existing.Email != email || existing.IsActive || existing.PasswordHash is not null)
                throw new InvalidOperationException("Demo identity was modified; refusing to overwrite it.");
            ids.Add(id);
        }
        var courses = await db.Courses.AsNoTracking().OrderBy(c => c.Id).Select(c => new { c.Id, c.Title }).ToArrayAsync(ct);
        (string Course, string Kind, string Title, string Body, bool Anonymous, string[] Replies)[] threads =
        [
            ("Machine Learning", "Question", "Why do we split the data before scaling?",
                "I understand train/test split, but I keep mixing up the order with StandardScaler. If the scaler doesn’t see the labels, how can fitting it on the whole dataset still cause leakage? A tiny example would help!", false,
                ["Even the test set’s mean gives your training process information it shouldn’t have. Keep the test data unseen.", "Split first, fit the scaler on X_train, then use that fitted scaler to transform both sets. A pipeline helps keep the order straight.", "So fitting another scaler on the test set would also be wrong because each set would use different statistics?", "Exactly. Transform the test set with the training statistics, without fitting again."]),
            ("", "Discussion", "Anyone up for a small study group this week?",
                "Thinking of a relaxed 45-minute session: bring one thing you understand and one question you’re stuck on. ML or cloud to start. Reply with what you’d want to revise; we can agree on a time here.", false,
                ["I’m in for ML! I can explain train/validation/test split. Feature encoding is my question.", "Cloud would be nice too. I can walk through roles versus users.", "Could we keep a short recap here for anyone who can’t join?"]),
            ("Machine Learning", "Study tip", "My tiny checklist for avoiding data leakage",
                "1. Split before learning anything from the data.\n2. Fit preprocessing on the training set.\n3. Keep the test set for final evaluation.\n4. Check whether a feature contains information from the future.\n\nI’m using this while revisiting the labs. What would you add?", false,
                ["Check duplicate rows before splitting. Very similar examples in both sets can give misleading scores.", "Use a pipeline for cross-validation so preprocessing is fitted inside each training fold."]),
            ("Architecture SOA", "Question", "REST and SOA: are they competing approaches?",
                "My notes describe SOA as organising services, then introduce REST constraints. Can a REST API be part of an SOA system? I’d like to understand the relationship before memorising definitions.", true,
                ["Yes. One describes organising capabilities into services; the other describes constraints for resource-oriented interactions.", "Drawing services separately from their HTTP endpoints helped me. An endpoint is not automatically a whole service."]),
            ("Virtualisation", "Discussion", "What finally made IAM roles click for you?",
                "For me it was imagining an application needing temporary permission, instead of copying a user’s credentials into it. I’m still comparing roles, users and policies. Any good memory tricks?", false,
                ["I separate who can assume the role from what it can do. Trust and permission policies answer different questions.", "An EC2 instance accessing S3 is a useful example to draw. Label where each permission belongs."]),
            ("Développement JEE", "Question", "DTOs or entities in an API response?",
                "For a small project it’s tempting to return the entity directly. Apart from hiding passwords, what do DTOs solve? Looking for examples that make the extra mapping feel worthwhile.", false,
                ["They let your public API change independently from your persistence model.", "Anonymous forum posts are a good example: keep the author privately in the database and exclude the ID from the public response.", "Also watch for lazy-loaded relationships and serialisation loops. A DTO makes the response shape explicit."]),
            ("Sécurité", "Study tip", "Authentication vs authorisation: my two-question test",
                "Who is making the request?\nAre they allowed to do THIS action on THIS resource?\n\nBeing signed in doesn’t mean I can read someone else’s private notes. Simple, but it keeps helping me spot missing checks.", false,
                ["The resource-specific part matters. A role check alone can miss ownership.", "That’s the distinction I kept forgetting. Thanks for putting it in plain words."]),
            ("Développement Mobile", "Question", "How are you handling loading and retry screens?",
                "Trying to avoid the blank-screen moment while a request runs. Do you keep old content visible on refresh, or replace it with a loader? Curious what feels best on a phone.", false,
                ["For the first load, a clear loading state. On refresh, I prefer keeping the existing list visible.", "Make retry work without losing the user’s input. That matters more than a fancy skeleton."]),
            ("Framework", "Discussion", "Explain a Spark transformation without the jargon",
                "My mental model: transformations describe the next dataset; actions ask Spark to actually produce a result. How would you explain lazy evaluation to a classmate?", false,
                ["Like writing a recipe before cooking it: you can combine the steps before starting the work.", "Nice analogy. An action can trigger all those earlier transformations, not just the single line you’re looking at."]),
            ("", "Question", "Understand the slides, freeze on exercises. Anyone else?",
                "Reading a solution makes sense, but starting from an empty file feels completely different. I’m practising without immediately checking the answer. What actually helped you bridge that gap?", true,
                ["Very normal for me. I start with a smaller exercise and write the steps in plain language before coding.", "Close the solution and rebuild it the next day. Recognising an answer and recalling the steps are different skills.", "Thanks. Going smaller instead of rereading everything sounds worth trying.", "We could use the study-group thread for this: one small exercise, then explain our approaches."])
        ];
        var seedTime = DateTimeOffset.UtcNow.AddMinutes(-20);
        for (var i = 0; i < threads.Length; i++)
        {
            var item = threads[i]; var postId = Id("thread-" + i);
            Guid? courseId = item.Course.Length == 0 ? null : courses.FirstOrDefault(c => c.Title.Contains(item.Course, StringComparison.OrdinalIgnoreCase))?.Id;
            if (!await db.CommunityPosts.AnyAsync(p => p.Id == postId, ct))
                db.CommunityPosts.Add(new CommunityPost
                {
                    Id = postId,
                    AuthorId = ids[i % ids.Count],
                    CourseId = courseId,
                    Title = item.Title,
                    Body = item.Body,
                    Kind = item.Kind,
                    IsAnonymous = item.Anonymous,
                    IsSample = true,
                    RequestKey = "seed-thread-" + i,
                    CreatedAt = seedTime.AddHours(-i * 3)
                });
            for (var j = 0; j < item.Replies.Length; j++)
            {
                var replyId = Id($"reply-{i}-{j}");
                if (!await db.CommunityPosts.AnyAsync(p => p.Id == replyId, ct))
                    db.CommunityPosts.Add(new CommunityPost
                    {
                        Id = replyId,
                        ParentId = postId,
                        CourseId = courseId,
                        AuthorId = j == 2 ? ids[i % ids.Count] : ids[(i + j + 1) % ids.Count],
                        Body = item.Replies[j],
                        IsAnonymous = item.Anonymous && j == 2,
                        IsSample = true,
                        RequestKey = $"seed-reply-{i}-{j}",
                        CreatedAt = seedTime.AddHours(-i * 3).AddMinutes(j * 3 + 2)
                    });
            }
        }
        await db.SaveChangesAsync(ct);
        var samplePosts = await db.CommunityPosts.Where(p => p.IsSample && ids.Contains(p.AuthorId)).OrderBy(p => p.Id).Select(p => p.Id).ToArrayAsync(ct);
        for (var i = 0; i < samplePosts.Length; i++)
            for (var j = 0; j < 3 + i % 5; j++)
            {
                var postId = samplePosts[i]; var userId = ids[(i + j) % ids.Count];
                if (!await db.CommunityVotes.AnyAsync(v => v.PostId == postId && v.UserId == userId, ct))
                    db.CommunityVotes.Add(new CommunityVote { PostId = postId, UserId = userId, Value = j == 6 ? -1 : 1 });
            }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (ids.Count, threads.Length, threads.Sum(t => t.Replies.Length), await db.CommunityVotes.CountAsync(v => samplePosts.Contains(v.PostId) && ids.Contains(v.UserId), ct));
    }
}
