using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Tenants.Infrastructure;

// Removes expired, never-accepted invitations. Scheduled by Hangfire.
public sealed class ExpiredInvitationCleanupJob
{
    private readonly TenantsDbContext _db;
    private readonly ILogger<ExpiredInvitationCleanupJob> _logger;

    public ExpiredInvitationCleanupJob(TenantsDbContext db, ILogger<ExpiredInvitationCleanupJob> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task CleanupAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;

        // Hard delete: an expired invitation has no history worth keeping, and ExecuteDeleteAsync skips
        // the soft-delete interceptor. IgnoreQueryFilters because the job has no tenant and covers them all.
        var deletedCount = await _db.Invitations
            .IgnoreQueryFilters()
            .Where(invitation => invitation.AcceptedAtUtc == null && invitation.ExpiresAtUtc < now)
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation("Expired invitation cleanup removed {DeletedCount} invitation(s)", deletedCount);
    }
}
