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
}
