using Ats.Modules.Interviews.Domain;
using Ats.Shared.Contracts.Interviews;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Interviews.Application;

// Read port for other modules; never exposes the Interview aggregate.
public sealed class InterviewDirectory : IInterviewDirectory
{
    private readonly IInterviewsDbContext _db;

    public InterviewDirectory(IInterviewsDbContext db) => _db = db;

    public async Task<int> CountUpcomingInterviewsAsync(
        DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        // Upcoming means still Scheduled and in the future.
        return await _db.Interviews
            .AsNoTracking()
            .CountAsync(
                i => i.Status == InterviewStatus.Scheduled && i.ScheduledAtUtc >= nowUtc,
                cancellationToken);
    }

    public async Task<IReadOnlyList<CandidateInterviewInfo>> GetForApplicationAsync(
        Guid tenantId, Guid applicationId, CancellationToken cancellationToken = default)
    {
        // Explicit tenant: the caller runs across tenants and has already checked this application
        // belongs to tenantId.
        return await _db.Interviews
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(i => i.TenantId == tenantId && i.ApplicationId == applicationId && !i.IsDeleted)
            .OrderBy(i => i.ScheduledAtUtc)
            .Select(i => new CandidateInterviewInfo(
                i.Id, i.ApplicationId, i.Type.ToString(), i.ScheduledAtUtc, i.DurationMinutes,
                i.Status.ToString(), i.RoomToken))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CandidateInterviewInfo>> GetForApplicationsAsync(
        IReadOnlyCollection<Guid> applicationIds, CancellationToken cancellationToken = default)
    {
        if (applicationIds.Count == 0)
            return [];

        // No tenant filter: the caller already resolved these ids to one candidate's applications.
        return await _db.Interviews
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(i => applicationIds.Contains(i.ApplicationId) && !i.IsDeleted)
            .OrderBy(i => i.ScheduledAtUtc)
            .Select(i => new CandidateInterviewInfo(
                i.Id, i.ApplicationId, i.Type.ToString(), i.ScheduledAtUtc, i.DurationMinutes,
                i.Status.ToString(), i.RoomToken))
            .ToListAsync(cancellationToken);
    }
}
