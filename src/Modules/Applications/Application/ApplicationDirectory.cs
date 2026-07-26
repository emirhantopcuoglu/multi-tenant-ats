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

    // The candidate join can't miss: an application is always created against a candidate in
    // this module's own schema. The job title comes through the Jobs read port because the
    // title lives in that module's schema — same one-hop rule the Interviews caller follows.
    // Both base queryables are either the ambient-tenant-filtered set (in-tenant caller) or an
    // explicit-tenant, filter-bypassing set (cross-tenant caller) — Candidate is tenant-scoped
    // too, so the join side needs the same bypass or it silently finds nothing.
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

    // Two queries total regardless of batch size: one join for the applications and their candidates,
    // one batched call into the Jobs port for the titles. The per-id overload above would have cost
    // two round trips per application instead — the N+1 this method exists to avoid.
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

        // A missing job title becomes an empty string, matching the single-id overload: consumers
        // already render that as "the role you applied for" rather than failing the message.
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
        // Ambient tenant filter applies: only this tenant's applications for the candidate. Soft
        // deletes are excluded by the same global filter (Application is ISoftDeletable).
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
