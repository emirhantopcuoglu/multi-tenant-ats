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
            // Per-tenant public listing: filter and ORDER BY both come from this index.
            entity.HasIndex(j => new { j.TenantId, j.Status, j.PublishedAtUtc })
                .IsDescending(false, false, true);

            // Cross-tenant feed, which can't use the TenantId-first index above.
            entity.HasIndex(j => new { j.Status, j.PublishedAtUtc })
                .IsDescending(false, true);

            entity.OwnsOne(j => j.SalaryRange, sr =>
            {
                sr.Property(p => p.Min).HasColumnName("SalaryMin");
                sr.Property(p => p.Max).HasColumnName("SalaryMax");
                sr.Property(p => p.Currency).HasColumnName("SalaryCurrency").HasMaxLength(3);
            });
        });

        // Called through an instance method so EF re-reads _currentTenant on every query.
        var applyFilter = GetType()
            .GetMethod(nameof(SetTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var entityType in builder.Model.GetEntityTypes()
            .Where(t => typeof(ITenantScoped).IsAssignableFrom(t.ClrType)))
        {
            applyFilter.MakeGenericMethod(entityType.ClrType).Invoke(this, [builder]);
        }
    }

    // EF allows one filter per entity, so tenant and soft delete share one predicate.
    private void SetTenantFilter<T>(ModelBuilder builder) where T : class, ITenantScoped, ISoftDeletable
        => builder.Entity<T>().HasQueryFilter(e => !e.IsDeleted && (Guid?)e.TenantId == _currentTenant.TenantId);
}
