using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Shared.Infrastructure;

// Every module's schema has its own AuditLogs table, so this is registered once per DbContext.
public sealed class AuditLogRetentionCleaner<TDbContext> : IRetentionCleaner
    where TDbContext : DbContext
{
    private readonly TDbContext _db;

    public AuditLogRetentionCleaner(TDbContext db) => _db = db;

    public Task<int> CleanAsync(RetentionCutoffs cutoffs, CancellationToken cancellationToken = default)
    {
        var before = cutoffs.AuditLogBeforeUtc;
        return _db.Set<AuditLogEntry>().Where(e => e.OccurredAtUtc < before).ExecuteDeleteAsync(cancellationToken);
    }
}
