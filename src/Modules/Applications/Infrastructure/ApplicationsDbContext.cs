using System.Reflection;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Domain;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema("applications");

        // Transactional outbox tables (OutboxMessage/OutboxState/InboxState). They live in this
        // context so an integration-event publish is written in the same transaction as the
        // business change; a background delivery service forwards them to RabbitMQ afterwards.
        // They are not ITenantScoped, so the tenant filter loop below leaves them untouched.
        builder.AddTransactionalOutboxEntities();

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

            // Full-text search vector (Sprint 6.4). A STORED generated column so PostgreSQL
            // maintains it automatically on every insert/update. The Domain entity stays clean
            // (no NpgsqlTsVector property); EF accesses it as a shadow property. The GIN index
            // makes @@ lookups O(log n) instead of O(n) sequential scans.
            entity.Property<NpgsqlTsVector>("SearchVector")
                .HasColumnType("tsvector")
                .HasComputedColumnSql(
                    "to_tsvector('english', coalesce(\"FirstName\",'') || ' ' || coalesce(\"LastName\",'') || ' ' || coalesce(\"Email\",''))",
                    stored: true);
            entity.HasIndex("SearchVector").HasMethod("GIN");
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
            // "One active application per (tenant, job, candidate)", the same rule Candidate above
            // enforces for (tenant, email). SubmitApplicationHandler's pre-check cannot hold on its
            // own: two concurrent submits both read "no application yet" and both insert. Partial,
            // because the rule is only about live applications — a candidate whose earlier
            // application was withdrawn or rejected is free to apply again, and the previous rows
            // must not block that. Named explicitly so it coexists with the plain lookup index
            // above, which still serves reads that span every status.
            entity.HasIndex(
                    a => new { a.TenantId, a.JobId, a.CandidateId },
                    "IX_Applications_TenantId_JobId_CandidateId_Active")
                .IsUnique()
                .HasFilter("\"Status\" = 'Active' AND NOT \"IsDeleted\"");
            entity.HasIndex(a => new { a.TenantId, a.JobId, a.CurrentStageId });
            // The recruiter list (ListApplications) always orders by AppliedAtUtc DESC. The default
            // and status-filtered views carry no JobId, so the (TenantId, JobId, ...) indexes above
            // cannot serve the sort; this one provides it from the index instead of a separate sort.
            entity.HasIndex(a => new { a.TenantId, a.AppliedAtUtc })
                .IsDescending(false, true);
            // Used by the candidate portal (7.9) to list a candidate's own applications across
            // all tenants. IgnoreQueryFilters() + this index serves the cross-tenant read.
            entity.HasIndex(a => a.CandidateAccountId)
                .HasFilter("\"CandidateAccountId\" IS NOT NULL");
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

        // The append-only activity log moved to MongoDB in Sprint 4, so every entity that remains
        // here is both tenant-scoped and soft-deletable: one filter covers both. Applied via an
        // instance method so EF treats _currentTenant as a context accessor (re-evaluated per
        // query) rather than baking the first scope's value into the cached model.
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
