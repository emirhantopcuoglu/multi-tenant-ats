using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Tenants.Infrastructure;

// Background maintenance job (Sprint 5.4): removes invitation tokens that have expired without being
// accepted. Scheduled by Hangfire from the composition root; the class itself has no Hangfire
// dependency, mirroring how consumers/handlers stay transport-agnostic.
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

        // Hard delete on purpose: an expired, never-accepted token is already unreachable (Invitation.IsValid
        // is false), so soft-deleting it would neither preserve useful history nor reclaim space — it would
        // defeat the point of a cleanup job. ExecuteDeleteAsync issues a single DELETE, so it also bypasses
        // the soft-delete interceptor (which only runs on SaveChanges) and never loads rows into memory.
        //
        // IgnoreQueryFilters is required: this runs without an HTTP request, so there is no current tenant for
        // the global filter to match. As a system-wide maintenance job it must span every tenant — the same
        // reason the invitation-accept flow ignores the filter.
        //
        // The predicate makes the job idempotent: a second run finds nothing left to delete.
        var deletedCount = await _db.Invitations
            .IgnoreQueryFilters()
            .Where(invitation => invitation.AcceptedAtUtc == null && invitation.ExpiresAtUtc < now)
            .ExecuteDeleteAsync(ct);

        _logger.LogInformation("Expired invitation cleanup removed {DeletedCount} invitation(s)", deletedCount);
    }
}
