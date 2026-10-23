using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UniCompanion.Core.Courses;
using UniCompanion.Core.Community;
using UniCompanion.Core.Study;
using UniCompanion.Infrastructure.Identity;

namespace UniCompanion.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<AppUser>(options)
{
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<CourseMembership> CourseMemberships => Set<CourseMembership>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<MaterialVersion> MaterialVersions => Set<MaterialVersion>();

    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    public DbSet<StudyJob> StudyJobs => Set<StudyJob>();
    public DbSet<StudyRequestKey> StudyRequestKeys => Set<StudyRequestKey>();
    public DbSet<StudyCheckpoint> StudyCheckpoints => Set<StudyCheckpoint>();
    public DbSet<QuizAttempt> QuizAttempts => Set<QuizAttempt>();

    public DbSet<CommunityPost> CommunityPosts => Set<CommunityPost>();
    public DbSet<CommunityVote> CommunityVotes => Set<CommunityVote>();
    public DbSet<ThreadSubscription> ThreadSubscriptions => Set<ThreadSubscription>();
    public DbSet<SavedItem> SavedItems => Set<SavedItem>();
    public DbSet<CommunityBlock> CommunityBlocks => Set<CommunityBlock>();
    public DbSet<CommunityReport> CommunityReports => Set<CommunityReport>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.Entity<ThreadSubscription>(e =>
        {
            e.HasKey(x => new { x.ThreadId, x.OwnerId });
            e.HasOne<CommunityPost>().WithMany().HasForeignKey(x => x.ThreadId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<SavedItem>(e =>
        {
            e.Property(x => x.Kind).HasMaxLength(20);
            e.HasIndex(x => new { x.OwnerId, x.Kind, x.TargetId }).IsUnique();
            e.HasIndex(x => new { x.OwnerId, x.CreatedAt, x.Id });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<CommunityBlock>(e =>
        {
            e.HasIndex(x => new { x.OwnerId, x.BlockedUserId, x.IsAnonymous }).IsUnique();
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.BlockedUserId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<CommunityReport>(e =>
        {
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.State).HasMaxLength(20);
            e.HasIndex(x => new { x.PostId, x.ReporterId }).IsUnique();
            e.HasIndex(x => new { x.State, x.CreatedAt });
            e.HasOne<CommunityPost>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ReporterId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<CommunityPost>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(180);
            e.Property(x => x.Body).HasMaxLength(8000);
            e.Property(x => x.Kind).HasMaxLength(20);
            e.Property(x => x.RequestKey).HasMaxLength(100);
            e.Property(x => x.RequestHash).HasMaxLength(64);
            e.HasIndex(x => new { x.AuthorId, x.CreatedAt, x.Id });
            e.HasIndex(x => new { x.AuthorId, x.RequestKey }).IsUnique();
            e.HasIndex(x => new { x.ParentId, x.CreatedAt });
            e.HasIndex(x => new { x.CourseId, x.CreatedAt });
            e.HasOne<CommunityPost>().WithMany().HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<CommunityVote>(e =>
        {
            e.HasKey(x => new { x.PostId, x.UserId });
            e.HasOne<CommunityPost>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("CK_CommunityVote_Value", "\"Value\" IN (-1, 1)"));
        });
        model.Entity<StudyJob>(e =>
        {
            e.Property(x => x.RequestJson).HasColumnType("jsonb");
            e.Property(x => x.ResultJson).HasColumnType("jsonb");
            e.Property(x => x.Fingerprint).HasMaxLength(64);
            e.HasIndex(x => new { x.OwnerId, x.Fingerprint });
            e.HasIndex(x => new { x.State, x.NextAttemptAt });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<StudyRequestKey>(e =>
        {
            e.HasKey(x => new { x.OwnerId, x.Key });
            e.Property(x => x.Key).HasMaxLength(100);
            e.HasOne<StudyJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<StudyCheckpoint>(e =>
        {
            e.HasKey(x => new { x.JobId, x.Key });
            e.Property(x => x.ResultJson).HasColumnType("jsonb");
            e.HasOne<StudyJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<QuizAttempt>(e =>
        {
            e.HasIndex(x => new { x.OwnerId, x.Key }).IsUnique();
            e.Property(x => x.Key).HasMaxLength(100);
            e.Property(x => x.ResultJson).HasColumnType("jsonb");
            e.HasOne<StudyJob>().WithMany().HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<DocumentChunk>(e =>
        {
            e.HasIndex(x => new { x.MaterialVersionId, x.Model, x.Ordinal }).IsUnique();
            e.Property(x => x.Embedding).HasColumnType("real[]");
            e.HasOne<MaterialVersion>().WithMany().HasForeignKey(x => x.MaterialVersionId).OnDelete(DeleteBehavior.Cascade);
            e.ToTable(t => t.HasCheckConstraint("CK_Chunk_VectorSize", "cardinality(\"Embedding\") = 384"));
        });
        model.HasPostgresExtension("vector");
        model.Entity<AppUser>().Property(x => x.DisplayName).HasMaxLength(80);
        model.Entity<Course>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(180);
            e.HasIndex(x => x.SourceId).IsUnique();
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.CreatorId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.SourceMetadataJson).HasColumnType("jsonb");
        });
        model.Entity<CourseMembership>(e =>
        {
            e.HasKey(x => new { x.CourseId, x.UserId });
            e.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<Material>(e =>
        {
            e.HasIndex(x => new { x.CourseId, x.SourceId }).IsUnique();
            e.HasOne<Course>().WithMany().HasForeignKey(x => x.CourseId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<MaterialVersion>().WithMany().HasForeignKey(x => x.CurrentVersionId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<MaterialVersion>(e =>
        {
            e.HasIndex(x => new { x.MaterialId, x.Sha256 }).IsUnique();
            e.Property(x => x.Sha256).HasMaxLength(64);
            e.HasOne<Material>().WithMany().HasForeignKey(x => x.MaterialId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
