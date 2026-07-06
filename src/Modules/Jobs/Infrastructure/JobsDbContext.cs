using System.Reflection;
using Ats.Modules.Jobs.Application;
using Ats.Modules.Jobs.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Jobs.Infrastructure;

public sealed class JobsDbContext : DbContext, IJobsDbContext
{
    private readonly ICurrentTenant _currentTenant;

    public JobsDbContext(DbContextOptions<JobsDbContext> options, ICurrentTenant currentTenant)
        : base(options)
    {
        _currentTenant = currentTenant;
    }

    public DbSet<Job> Jobs => Set<Job>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("jobs");

        builder.Entity<Job>(entity =>
        {
            entity.HasKey(j => j.Id);
            entity.Property(j => j.Title).IsRequired().HasMaxLength(200);
            entity.Property(j => j.Description).IsRequired();
            entity.Property(j => j.Department).HasMaxLength(100);
            entity.Property(j => j.City).HasMaxLength(200);
            entity.Property(j => j.Country).HasMaxLength(100);
            entity.Property(j => j.Slug).IsRequired().HasMaxLength(250);
            entity.Property(j => j.EmploymentType).HasConversion<string>();
            entity.Property(j => j.ExperienceLevel).HasConversion<string>();
            entity.Property(j => j.WorkArrangement).HasConversion<string>();
            entity.Property(j => j.Status).HasConversion<string>();
            entity.HasIndex(j => new { j.TenantId, j.Slug }).IsUnique();
            // Serves the hot public listing (ListPublicJobs): filter on (TenantId, Status=Published)
            // is the index prefix, and PublishedAtUtc DESC is the ORDER BY — both come from this one
            // index, so the planner needs no separate sort. It supersedes a plain (TenantId, Status)
            // index, which would be a redundant prefix of this one.
            entity.HasIndex(j => new { j.TenantId, j.Status, j.PublishedAtUtc })
                .IsDescending(false, false, true);

            // Serves the cross-tenant marketplace feed (ListPublicJobFeed): unlike the per-tenant
            // listing above, it filters on Status=Published across ALL tenants and orders by
            // PublishedAtUtc DESC, so the leading TenantId of that index cannot be used. This one
            // starts at Status, matching the feed's filter + sort with no separate sort step.
            entity.HasIndex(j => new { j.Status, j.PublishedAtUtc })
                .IsDescending(false, true);

            entity.OwnsOne(j => j.SalaryRange, sr =>
            {
                sr.Property(p => p.Min).HasColumnName("SalaryMin");
                sr.Property(p => p.Max).HasColumnName("SalaryMax");
                sr.Property(p => p.Currency).HasColumnName("SalaryCurrency").HasMaxLength(3);
            });
        });

        // Register the filter via an instance method so EF Core treats _currentTenant as a
        // context accessor (re-evaluated per query) rather than baking the first scope's
        // value into the cached model. A captured delegate would leak across tenants.
        var applyFilter = GetType()
            .GetMethod(nameof(SetTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var entityType in builder.Model.GetEntityTypes()
            .Where(t => typeof(ITenantScoped).IsAssignableFrom(t.ClrType)))
        {
            applyFilter.MakeGenericMethod(entityType.ClrType).Invoke(this, [builder]);
        }
    }

    // Combined global filter: hide rows of other tenants AND soft-deleted rows. EF Core
    // allows only one query filter per entity, so both conditions live in one predicate.
    // Written as a literal lambda in an instance method so EF re-evaluates _currentTenant
    // per query (a captured delegate would bake the first scope's tenant into the model).
    private void SetTenantFilter<T>(ModelBuilder builder) where T : class, ITenantScoped, ISoftDeletable
        => builder.Entity<T>().HasQueryFilter(e => !e.IsDeleted && (Guid?)e.TenantId == _currentTenant.TenantId);
}
