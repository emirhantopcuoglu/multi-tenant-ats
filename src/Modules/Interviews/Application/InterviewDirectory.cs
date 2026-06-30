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
}
