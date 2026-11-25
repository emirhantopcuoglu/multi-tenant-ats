using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Tenants.Infrastructure;

// Called when a company JWT is authenticated, so a deactivated user is shut out right away instead of
// when the token expires.
public sealed class CompanyUserStatusValidator
{
    private readonly TenantsDbContext _db;

    public CompanyUserStatusValidator(TenantsDbContext db)
    {
        _db = db;
    }

    public async Task<bool> IsActiveAsync(ClaimsPrincipal user)
    {
        if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return false;

        // Runs on every request, so read one column. A missing user reads as inactive.
        return await _db.Users
            .Where(u => u.Id == userId)
            .AnyAsync(u => u.DeactivatedAtUtc == null);
    }
}
