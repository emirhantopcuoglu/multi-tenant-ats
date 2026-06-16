using System.Reflection;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

// The aggregate and this module's Application-layer namespace are both called "Application";
// the alias disambiguates the type from the namespace.
using ApplicationEntity = Ats.Modules.Applications.Domain.Application;

namespace Ats.Modules.Applications.Infrastructure;

public sealed class ApplicationsDbContext : DbContext, IApplicationsDbContext
{
    private readonly ICurrentTenant _currentTenant;

    public ApplicationsDbContext(DbContextOptions<ApplicationsDbContext> options, ICurrentTenant currentTenant)
        : base(options)
    {
        _currentTenant = currentTenant;
    }

    public DbSet<ApplicationEntity> Applications => Set<ApplicationEntity>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<Pipeline> Pipelines => Set<Pipeline>();
    public DbSet<PipelineStage> PipelineStages => Set<PipelineStage>();
    public DbSet<ApplicationActivity> Activities => Set<ApplicationActivity>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("applications");

        builder.Entity<Candidate>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Email).IsRequired().HasMaxLength(256);
            entity.Property(c => c.FirstName).IsRequired().HasMaxLength(100);
            entity.Property(c => c.LastName).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Phone).HasMaxLength(40);
            entity.Property(c => c.LinkedInUrl).HasMaxLength(300);
            // Enforces the "one candidate per (tenant, email)" rule at the database, the only
            // place that holds under concurrent inserts.
            entity.HasIndex(c => new { c.TenantId, c.Email }).IsUnique();
        });

        builder.Entity<ApplicationEntity>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.CvFileKey).IsRequired().HasMaxLength(512);
            entity.Property(a => a.CoverLetter).HasMaxLength(5000);
            entity.Property(a => a.RejectionReason).HasMaxLength(1000);
            entity.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
            // Cross-aggregate references (JobId, CandidateId, CurrentStageId) are stored as plain
            // ids with no FK navigation — the aggregates stay independent. Indexed for the
            // recruiter list/filter queries that arrive in step 3.4.
            entity.HasIndex(a => new { a.TenantId, a.JobId, a.CandidateId });
            entity.HasIndex(a => new { a.TenantId, a.JobId, a.CurrentStageId });
        });

        builder.Entity<Pipeline>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => new { p.TenantId, p.JobId }).IsUnique();

            // Pipeline owns its stages. The collection is exposed read-only via a backing
            // field, so EF reads and writes _stages directly rather than the property.
            entity.HasMany(p => p.Stages)
                .WithOne()
                .HasForeignKey(s => s.PipelineId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Navigation(p => p.Stages)
                .UsePropertyAccessMode(PropertyAccessMode.Field);
            entity.Metadata.FindNavigation(nameof(Pipeline.Stages))!.SetField("_stages");
        });

        builder.Entity<PipelineStage>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Name).IsRequired().HasMaxLength(100);
            entity.Property(s => s.Type).HasConversion<string>().HasMaxLength(20);
        });

        builder.Entity<ApplicationActivity>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.ActivityType).HasConversion<string>().HasMaxLength(30);
            // The schema-flexible part: stored as jsonb so payloads of different shapes share
            // one column and can be queried by key later (and migrate cleanly to MongoDB).
            entity.Property(a => a.Payload).HasColumnType("jsonb").IsRequired();
            // Reading an application's timeline newest-first is the access pattern.
            entity.HasIndex(a => new { a.TenantId, a.ApplicationId, a.OccurredAtUtc });
        });

        // Apply the global filter via instance methods so EF treats _currentTenant as a context
        // accessor (re-evaluated per query) rather than baking the first scope's value into the
        // cached model. Every tenant-scoped entity gets a tenant filter; those that are also
        // soft-deletable get the IsDeleted half too. The append-only activity log is
        // tenant-scoped but not soft-deletable, hence the split.
        var tenantAndSoftDelete = GetType()
            .GetMethod(nameof(SetTenantAndSoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;
        var tenantOnly = GetType()
            .GetMethod(nameof(SetTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

        foreach (var entityType in builder.Model.GetEntityTypes()
            .Where(t => typeof(ITenantScoped).IsAssignableFrom(t.ClrType)))
        {
            var method = typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType)
                ? tenantAndSoftDelete
                : tenantOnly;
            method.MakeGenericMethod(entityType.ClrType).Invoke(this, [builder]);
        }
    }

    private void SetTenantAndSoftDeleteFilter<T>(ModelBuilder builder)
        where T : class, ITenantScoped, ISoftDeletable
        => builder.Entity<T>().HasQueryFilter(e => !e.IsDeleted && (Guid?)e.TenantId == _currentTenant.TenantId);

    private void SetTenantFilter<T>(ModelBuilder builder) where T : class, ITenantScoped
        => builder.Entity<T>().HasQueryFilter(e => (Guid?)e.TenantId == _currentTenant.TenantId);
}
