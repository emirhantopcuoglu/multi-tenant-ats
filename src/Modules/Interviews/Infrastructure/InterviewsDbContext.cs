using System.Reflection;
using Ats.Modules.Interviews.Application;
using Ats.Modules.Interviews.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Interviews.Infrastructure;

public sealed class InterviewsDbContext : DbContext, IInterviewsDbContext
{
    private readonly ICurrentTenant _currentTenant;

    public InterviewsDbContext(DbContextOptions<InterviewsDbContext> options, ICurrentTenant currentTenant)
        : base(options)
    {
        _currentTenant = currentTenant;
    }

    public DbSet<Interview> Interviews => Set<Interview>();
    public DbSet<InterviewFeedback> Feedback => Set<InterviewFeedback>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("interviews");

        builder.Entity<Interview>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Type).HasConversion<string>().HasMaxLength(20);
            entity.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(i => i.Notes).HasMaxLength(5000);
            // Stored as text like Type and Status: an interview's history outlives any one release,
            // and a string keeps a dump readable without the enum's ordinals to hand.
            entity.Property(i => i.CancellationReason).HasConversion<string>().HasMaxLength(30);
            entity.Property(i => i.CancellationNote).HasMaxLength(500);
            entity.Property(i => i.NoShowParty).HasConversion<string>().HasMaxLength(20);
            // Nullable: a phone screen has no live room, so no token. A PostgreSQL unique index
            // treats NULLs as distinct, so many phone screens coexist while real tokens stay unique.
            entity.Property(i => i.RoomToken).HasMaxLength(64);

            // Looked up directly by token (the join endpoint has no tenant context yet — the token
            // itself is what identifies the interview), so it must be globally unique, not just
            // unique within a tenant.
            entity.HasIndex(i => i.RoomToken).IsUnique();

            // The interviewers live in a native uuid[] column, read/written through the backing field.
            entity.PrimitiveCollection(i => i.InterviewerUserIds)
                .HasField("_interviewerUserIds")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            // The schedule view filters by date range and orders by time; this serves both from the index.
            entity.HasIndex(i => new { i.TenantId, i.ScheduledAtUtc });
            // "Interviews for this interviewer" is an array-membership test (= ANY); a GIN index on the
            // uuid[] column is what makes that lookup fast.
            entity.HasIndex(i => i.InterviewerUserIds).HasMethod("gin");

            // The reminder sweep asks "which reminders are owed right now" across every tenant, so
            // these are indexed on the due column alone — a TenantId-prefixed index could not serve
            // that query. Partial, because the column is nulled once the reminder is settled: the
            // index then holds only pending reminders (a handful at any instant) instead of a row per
            // interview ever scheduled.
            entity.HasIndex(i => i.DayBeforeReminderDueAtUtc)
                .HasFilter("\"DayBeforeReminderDueAtUtc\" IS NOT NULL");
            entity.HasIndex(i => i.StartingSoonReminderDueAtUtc)
                .HasFilter("\"StartingSoonReminderDueAtUtc\" IS NOT NULL");
        });

        builder.Entity<InterviewFeedback>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.ToTable("feedback");
            entity.Property(f => f.Recommendation).HasConversion<string>().HasMaxLength(20);
            entity.Property(f => f.Comments).HasMaxLength(5000);

            entity.HasIndex(f => f.InterviewId);
            // One feedback per interviewer per interview; the DB index is the authoritative guard.
            entity.HasIndex(f => new { f.InterviewId, f.InterviewerUserId }).IsUnique();
        });

        // Two filter helpers: one for entities that are both tenant-scoped and soft-deletable, one for
        // entities that are tenant-scoped only. Both are applied via instance methods so EF re-evaluates
        // _currentTenant on every query rather than baking the first scope's value into the model.
        var withSoftDelete = GetType()
            .GetMethod(nameof(SetTenantAndSoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;
        var tenantOnly = GetType()
            .GetMethod(nameof(SetTenantOnlyFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var entityType in builder.Model.GetEntityTypes()
            .Where(t => typeof(ITenantScoped).IsAssignableFrom(t.ClrType)))
        {
            var method = typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType)
                ? withSoftDelete
                : tenantOnly;

            method.MakeGenericMethod(entityType.ClrType).Invoke(this, [builder]);
        }
    }

    private void SetTenantAndSoftDeleteFilter<T>(ModelBuilder builder) where T : class, ITenantScoped, ISoftDeletable
        => builder.Entity<T>().HasQueryFilter(e => !e.IsDeleted && (Guid?)e.TenantId == _currentTenant.TenantId);

    private void SetTenantOnlyFilter<T>(ModelBuilder builder) where T : class, ITenantScoped
        => builder.Entity<T>().HasQueryFilter(e => (Guid?)e.TenantId == _currentTenant.TenantId);
}
