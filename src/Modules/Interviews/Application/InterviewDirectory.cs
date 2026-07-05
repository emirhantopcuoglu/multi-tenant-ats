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
                i.Id, i.Type.ToString(), i.ScheduledAtUtc, i.DurationMinutes, i.Location,
                i.Status.ToString()))
            .ToListAsync(cancellationToken);
    }
}
