using Ats.Modules.Applications.Domain;
using Ats.Shared.Contracts.Applications;
using Ats.Shared.Contracts.Jobs;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Applications.Application;

// The Applications module's implementation of the cross-module read port, the counterpart to the
// Jobs module's JobDirectory. It answers the Interviews module's questions about applications —
// returning flat read models, never the Application aggregate. Tenant scoping is automatic via the
// global query filter on the context.
public sealed class ApplicationDirectory : IApplicationDirectory
{
    private readonly IApplicationsDbContext _db;
    private readonly IJobDirectory _jobs;

    public ApplicationDirectory(IApplicationsDbContext db, IJobDirectory jobs)
    {
        _db = db;
        _jobs = jobs;
    }

    public async Task<ApplicationForScheduling?> GetForSchedulingAsync(
        Guid applicationId, CancellationToken cancellationToken = default)
    {
        // The candidate join can't miss: an application is always created against a candidate in
        // this module's own schema. The job title comes through the Jobs read port because the
        // title lives in that module's schema — same one-hop rule the Interviews caller follows.
        var row = await (
            from a in _db.Applications.AsNoTracking()
            where a.Id == applicationId
            join c in _db.Candidates.AsNoTracking() on a.CandidateId equals c.Id
            select new
            {
                a.Id,
                IsActive = a.Status == ApplicationStatus.Active,
                a.JobId,
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
            row.CandidateId, row.Email, row.FirstName);
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

    public async Task<int> CountApplicationsSinceAsync(
        DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        return await _db.Applications
            .AsNoTracking()
            .CountAsync(a => a.AppliedAtUtc >= sinceUtc, cancellationToken);
    }

    public async Task<int> CountActiveCandidatesAsync(CancellationToken cancellationToken = default)
    {
        // Distinct candidates, not applications: one candidate may hold several open applications but
        // is a single "active candidate". Distinct + count is one aggregate query (no N+1).
        return await _db.Applications
            .AsNoTracking()
            .Where(a => a.Status == ApplicationStatus.Active)
            .Select(a => a.CandidateId)
            .Distinct()
            .CountAsync(cancellationToken);
    }
}
