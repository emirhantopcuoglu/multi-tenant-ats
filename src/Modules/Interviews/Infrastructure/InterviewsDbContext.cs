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
            entity.Property(i => i.CancellationReason).HasConversion<string>().HasMaxLength(30);
            entity.Property(i => i.CancellationNote).HasMaxLength(500);
            entity.Property(i => i.NoShowParty).HasConversion<string>().HasMaxLength(20);
            // Null for phone screens. Postgres treats NULLs as distinct in a unique index.
            entity.Property(i => i.RoomToken).HasMaxLength(64);

            // Globally unique: the join endpoint looks it up without a tenant.
            entity.HasIndex(i => i.RoomToken).IsUnique();

            entity.PrimitiveCollection(i => i.InterviewerUserIds)
                .HasField("_interviewerUserIds")
                .UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.HasIndex(i => new { i.TenantId, i.ScheduledAtUtc });
            // GIN index for the "= ANY" interviewer lookup.
            entity.HasIndex(i => i.InterviewerUserIds).HasMethod("gin");

            // The sweep runs across tenants, so these aren't tenant-prefixed. Partial: only pending reminders.
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
            entity.HasIndex(f => new { f.InterviewId, f.InterviewerUserId }).IsUnique();
        });

        // Called through instance methods so EF re-reads _currentTenant on every query.
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
