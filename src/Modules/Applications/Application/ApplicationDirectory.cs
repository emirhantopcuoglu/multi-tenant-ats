using Ats.Modules.Applications.Domain;
using Ats.Shared.Contracts.Applications;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Applications.Application;

// The Applications module's implementation of the cross-module read port, the counterpart to the
// Jobs module's JobDirectory. It answers one question for the Interviews module — "is there an
// application with this id, and is it still open?" — returning a flat read model, never the
// Application aggregate. Tenant scoping is automatic via the global query filter on the context.
public sealed class ApplicationDirectory : IApplicationDirectory
{
    private readonly IApplicationsDbContext _db;

    public ApplicationDirectory(IApplicationsDbContext db) => _db = db;

    public async Task<ApplicationForScheduling?> GetForSchedulingAsync(
        Guid applicationId, CancellationToken cancellationToken = default)
    {
        return await _db.Applications
            .AsNoTracking()
            .Where(a => a.Id == applicationId)
            .Select(a => new ApplicationForScheduling(a.Id, a.Status == ApplicationStatus.Active))
            .FirstOrDefaultAsync(cancellationToken);
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
}
