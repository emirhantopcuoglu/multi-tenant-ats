using Ats.Modules.Applications.Domain;
using Ats.Shared.Contracts.Applications;
using Ats.Shared.Contracts.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Applications.Application;

// Read port for other modules. Returns flat read models, never the Application aggregate.
public sealed class ApplicationDirectory : IApplicationDirectory
{
    private readonly IApplicationsDbContext _db;
    private readonly IJobDirectory _jobs;

    public ApplicationDirectory(IApplicationsDbContext db, IJobDirectory jobs)
    {
        _db = db;
        _jobs = jobs;
    }

    public Task<ApplicationForScheduling?> GetForSchedulingAsync(
        Guid applicationId, CancellationToken cancellationToken = default) =>
        GetForSchedulingAsync(
            _db.Applications.AsNoTracking(), _db.Candidates.AsNoTracking(), applicationId, cancellationToken);

    public Task<ApplicationForScheduling?> GetForSchedulingAsync(
        Guid tenantId, Guid applicationId, CancellationToken cancellationToken = default) =>
        GetForSchedulingAsync(
            _db.Applications.AsNoTracking().IgnoreQueryFilters().Where(a => a.TenantId == tenantId),
            _db.Candidates.AsNoTracking().IgnoreQueryFilters().Where(c => c.TenantId == tenantId),
            applicationId, cancellationToken);

    // The job title comes from the Jobs port. For cross-tenant callers both queries bypass the
    // filter and match the tenant explicitly, Candidate included, or the join finds nothing.
    private async Task<ApplicationForScheduling?> GetForSchedulingAsync(
        IQueryable<Domain.Application> applications, IQueryable<Candidate> candidates,
        Guid applicationId, CancellationToken cancellationToken)
    {
        var row = await (
            from a in applications
            where a.Id == applicationId
            join c in candidates on a.CandidateId equals c.Id
            select new
            {
                a.Id,
                IsActive = a.Status == ApplicationStatus.Active,
                a.JobId,
                a.CandidateAccountId,
                CandidateId = c.Id,
                c.Email,
                c.FirstName
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return null;

        var jobTitle = await _jobs.GetJobTitleByIdAsync(row.JobId, cancellationToken);
        return new ApplicationForScheduling(
            row.Id, row.IsActive, row.JobId, jobTitle ?? string.Empty,
            row.CandidateId, row.CandidateAccountId, row.Email, row.FirstName);
    }

    // Two queries for any batch size, instead of two per application.
    public async Task<IReadOnlyDictionary<Guid, ApplicationForScheduling>> GetForSchedulingAsync(
        IReadOnlyCollection<Guid> applicationIds, CancellationToken cancellationToken = default)
    {
        if (applicationIds.Count == 0)
            return new Dictionary<Guid, ApplicationForScheduling>();

        var rows = await (
            from a in _db.Applications.AsNoTracking().IgnoreQueryFilters()
            where applicationIds.Contains(a.Id)
            join c in _db.Candidates.AsNoTracking().IgnoreQueryFilters() on a.CandidateId equals c.Id
            select new
            {
                a.Id,
                IsActive = a.Status == ApplicationStatus.Active,
                a.JobId,
                a.CandidateAccountId,
                CandidateId = c.Id,
                c.Email,
                c.FirstName
            })
            .ToListAsync(cancellationToken);

        var jobs = await _jobs.GetSummariesAsync(
            rows.Select(row => row.JobId).Distinct().ToList(), cancellationToken);

        // A missing title becomes an empty string; consumers fall back to a generic phrase.
        return rows.ToDictionary(
            row => row.Id,
            row => new ApplicationForScheduling(
                row.Id, row.IsActive, row.JobId,
                jobs.TryGetValue(row.JobId, out var job) ? job.Title : string.Empty,
                row.CandidateId, row.CandidateAccountId, row.Email, row.FirstName));
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetCandidateNamesByApplicationAsync(
        IReadOnlyCollection<Guid> applicationIds, CancellationToken cancellationToken = default)
    {
        if (applicationIds.Count == 0)
            return new Dictionary<Guid, string>();

        var pairs = await (
            from a in _db.Applications.AsNoTracking()
            where applicationIds.Contains(a.Id)
            join c in _db.Candidates.AsNoTracking() on a.CandidateId equals c.Id
            select new { a.Id, FullName = c.FirstName + " " + c.LastName })
            .ToListAsync(cancellationToken);

        return pairs.ToDictionary(pair => pair.Id, pair => pair.FullName);
    }

    public async Task<IReadOnlyList<Guid>> GetApplicationIdsForCandidateAsync(
        Guid candidateId, CancellationToken cancellationToken = default)
    {
        return await _db.Applications
            .AsNoTracking()
            .Where(a => a.CandidateId == candidateId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountApplicationsSinceAsync(
        DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        return await _db.Applications
            .AsNoTracking()
            .CountAsync(a => a.AppliedAtUtc >= sinceUtc, cancellationToken);
    }

    public async Task<int> CountActiveCandidatesAsync(CancellationToken cancellationToken = default)
    {
        // Counts distinct candidates, not applications.
        return await _db.Applications
            .AsNoTracking()
            .Where(a => a.Status == ApplicationStatus.Active)
            .Select(a => a.CandidateId)
            .Distinct()
            .CountAsync(cancellationToken);
    }
}
