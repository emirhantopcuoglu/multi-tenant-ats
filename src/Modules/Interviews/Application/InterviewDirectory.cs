using Ats.Modules.Interviews.Domain;
using Ats.Shared.Contracts.Interviews;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Interviews.Application;

// The Interviews module's implementation of the cross-module read port. It answers count questions
// for the dashboard without exposing the Interview aggregate. Tenant scoping is automatic: the
// global query filter on IInterviewsDbContext restricts every query to the current tenant.
public sealed class InterviewDirectory : IInterviewDirectory
{
    private readonly IInterviewsDbContext _db;

    public InterviewDirectory(IInterviewsDbContext db) => _db = db;

    public async Task<int> CountUpcomingInterviewsAsync(
        DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        // "Upcoming" = still Scheduled and not yet in the past. A single tenant-scoped COUNT.
        return await _db.Interviews
            .AsNoTracking()
            .CountAsync(
                i => i.Status == InterviewStatus.Scheduled && i.ScheduledAtUtc >= nowUtc,
                cancellationToken);
    }

    public async Task<IReadOnlyList<CandidateInterviewInfo>> GetForApplicationAsync(
        Guid tenantId, Guid applicationId, CancellationToken cancellationToken = default)
    {
        // Explicit tenant + IgnoreQueryFilters, not the ambient global filter: the caller (the
        // candidate-detail query) runs cross-tenant and has already verified this applicationId
        // belongs to tenantId before calling here.
        return await _db.Interviews
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId && i.ApplicationId == applicationId && !i.IsDeleted)
            .OrderBy(i => i.ScheduledAtUtc)
            .Select(i => new CandidateInterviewInfo(
                i.Id, i.ApplicationId, i.Type.ToString(), i.ScheduledAtUtc, i.DurationMinutes,
                i.Location, i.Status.ToString(), i.RoomToken))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CandidateInterviewInfo>> GetForApplicationsAsync(
        IReadOnlyCollection<Guid> applicationIds, CancellationToken cancellationToken = default)
    {
        if (applicationIds.Count == 0)
            return [];

        // No tenant filter at all, not even bypassed-and-matched: applicationIds is a set the
        // caller already resolved to one specific candidate account across every tenant that
        // account has applied to, so an interview id collision across tenants isn't possible —
        // matching purely on ApplicationId is both sufficient and the same trust boundary
        // GetCandidateNamesByApplicationAsync already relies on.
        return await _db.Interviews
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(i => applicationIds.Contains(i.ApplicationId) && !i.IsDeleted)
            .OrderBy(i => i.ScheduledAtUtc)
            .Select(i => new CandidateInterviewInfo(
                i.Id, i.ApplicationId, i.Type.ToString(), i.ScheduledAtUtc, i.DurationMinutes,
                i.Location, i.Status.ToString(), i.RoomToken))
            .ToListAsync(cancellationToken);
    }
}
