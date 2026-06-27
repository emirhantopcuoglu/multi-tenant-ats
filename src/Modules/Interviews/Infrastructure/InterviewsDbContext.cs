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
            entity.Property(i => i.Location).HasMaxLength(300);
            entity.Property(i => i.Notes).HasMaxLength(5000);

            // The interviewers live in a native uuid[] column, read/written through the backing field.
            entity.PrimitiveCollection(i => i.InterviewerUserIds)
                .HasField("_interviewerUserIds")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            // The schedule view filters by date range and orders by time; this serves both from the index.
            entity.HasIndex(i => new { i.TenantId, i.ScheduledAtUtc });
            // "Interviews for this interviewer" is an array-membership test (= ANY); a GIN index on the
            // uuid[] column is what makes that lookup fast.
            entity.HasIndex(i => i.InterviewerUserIds).HasMethod("gin");
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
