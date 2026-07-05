using Ats.Modules.Jobs.Domain;
using Ats.Shared.Contracts.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Jobs.Application;

// The Jobs module's implementation of the cross-module read port. It answers a single
// question for other modules — "is there a published job at this slug?" — and returns a flat
// read model, never the Job entity. Tenant scoping is automatic: IJobsDbContext applies the
// global query filter, so this only ever sees the current tenant's jobs.
public sealed class JobDirectory : IJobDirectory
{
    private readonly IJobsDbContext _db;

    public JobDirectory(IJobsDbContext db) => _db = db;

    public async Task<PublishedJob?> GetPublishedJobBySlugAsync(
        string slug, CancellationToken cancellationToken = default)
    {
        return await _db.Jobs
            .AsNoTracking()
            .Where(j => j.Slug == slug && j.Status == JobStatus.Published)
            .Select(j => new PublishedJob(j.Id, j.Title, j.Slug))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<string?> GetJobTitleByIdAsync(
        Guid jobId, CancellationToken cancellationToken = default)
    {
        // No status filter: a rejected application's job may be Closed or Archived, but we still
        // want its title for the email. The global query filter keeps this scoped to the tenant.
        return await _db.Jobs
            .AsNoTracking()
            .Where(j => j.Id == jobId)
            .Select(j => j.Title)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> CountOpenJobsAsync(CancellationToken cancellationToken = default)
    {
        // A single tenant-scoped COUNT — "open" means Published. No N+1: one aggregate query.
        return await _db.Jobs
            .AsNoTracking()
            .CountAsync(j => j.Status == JobStatus.Published, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, JobSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> jobIds, CancellationToken cancellationToken = default)
    {
        if (jobIds.Count == 0)
            return new Dictionary<Guid, JobSummary>();

        // IgnoreQueryFilters bypasses the tenant global filter — intentional, because a
        // candidate's applications can span multiple companies.
        return await _db.Jobs
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(j => jobIds.Contains(j.Id) && !j.IsDeleted)
            .Select(j => new JobSummary(j.Id, j.Title, j.Slug, j.TenantId))
            .ToDictionaryAsync(j => j.Id, cancellationToken);
    }

    public async Task<JobRequirements?> GetJobRequirementsAsync(
        Guid tenantId, Guid jobId, CancellationToken cancellationToken = default)
    {
        // IgnoreQueryFilters + an explicit tenantId match, same reasoning as GetSummariesAsync: the
        // caller here is a message consumer with no resolved ICurrentTenant to drive the global filter.
        return await _db.Jobs
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(j => j.TenantId == tenantId && j.Id == jobId && !j.IsDeleted)
            .Select(j => new JobRequirements(j.Title, j.Description))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
