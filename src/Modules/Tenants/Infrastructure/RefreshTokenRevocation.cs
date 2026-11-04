using Ats.Modules.Tenants.Domain;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Tenants.Infrastructure;

internal static class RefreshTokenRevocation
{
    // One UPDATE instead of tracked entities: it skips the xmin check, so a refresh rotating one of
    // these tokens at the same moment can't fail the whole revocation.
    public static Task<int> RevokeActiveAsync(
        this IQueryable<RefreshToken> tokens, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return tokens
            .Where(t => t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAtUtc, now), ct);
    }
}
