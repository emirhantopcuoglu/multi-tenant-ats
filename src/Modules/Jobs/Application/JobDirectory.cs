using Ats.Modules.Jobs.Domain;
using Ats.Shared.Contracts.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Jobs.Application;

// Read port for other modules. Returns flat read models, never the Job entity.
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
        // No status filter: the job of a rejected application may already be closed or archived.
        return await _db.Jobs
            .AsNoTracking()
            .Where(j => j.Id == jobId)
            .Select(j => j.Title)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> CountOpenJobsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Jobs
            .AsNoTracking()
            .CountAsync(j => j.Status == JobStatus.Published, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, JobSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> jobIds, CancellationToken cancellationToken = default)
    {
        if (jobIds.Count == 0)
            return new Dictionary<Guid, JobSummary>();

        // Ignores the tenant filter: a candidate's applications span companies.
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
        // Called from a consumer with no current tenant, so the tenant id is matched explicitly.
        return await _db.Jobs
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(j => j.TenantId == tenantId && j.Id == jobId && !j.IsDeleted)
            .Select(j => new JobRequirements(j.Title, j.Description))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
