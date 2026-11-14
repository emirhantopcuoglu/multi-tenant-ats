using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Tenants.Infrastructure;

// Expired company sessions and invitations. Runs with no tenant, so the invitation filter is bypassed.
public sealed class TenantsRetentionCleaner : IRetentionCleaner
{
    private readonly TenantsDbContext _db;

    public TenantsRetentionCleaner(TenantsDbContext db) => _db = db;

    public async Task<int> CleanAsync(RetentionCutoffs cutoffs, CancellationToken cancellationToken = default)
    {
        var expiredBefore = cutoffs.ExpiredTokenBeforeUtc;

        var sessions = await _db.RefreshTokens
            .Where(t => t.ExpiresAtUtc < expiredBefore)
            .ExecuteDeleteAsync(cancellationToken);

        // Accepted ones too: the email lives on the user now. ExecuteDelete skips the soft delete.
        var invitations = await _db.Invitations
            .IgnoreQueryFilters()
            .Where(i => i.ExpiresAtUtc < expiredBefore)
            .ExecuteDeleteAsync(cancellationToken);

        return sessions + invitations;
    }
}
