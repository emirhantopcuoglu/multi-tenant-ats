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
}
