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

        // Interview is both tenant-scoped and soft-deletable, so one filter covers both. Applied via an
        // instance method so EF treats _currentTenant as a context accessor (re-evaluated per query)
        // rather than baking the first scope's value into the cached model.
        var setFilter = GetType()
            .GetMethod(nameof(SetTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var entityType in builder.Model.GetEntityTypes()
            .Where(t => typeof(ITenantScoped).IsAssignableFrom(t.ClrType)))
        {
            setFilter.MakeGenericMethod(entityType.ClrType).Invoke(this, [builder]);
        }
    }

    private void SetTenantFilter<T>(ModelBuilder builder) where T : class, ITenantScoped, ISoftDeletable
        => builder.Entity<T>().HasQueryFilter(e => !e.IsDeleted && (Guid?)e.TenantId == _currentTenant.TenantId);
}
