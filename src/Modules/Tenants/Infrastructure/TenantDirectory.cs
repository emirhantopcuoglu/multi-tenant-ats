using Ats.Shared.Contracts.Tenants;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Tenants.Infrastructure;

// The Tenants module's implementation of the cross-module read port. It answers only what the public
// marketplace needs — name a batch of companies, or find companies by name — and returns flat read
// models, never the Tenant entity. These reads span all tenants intentionally (the Tenant table has
// no query filter), which is what makes the global job feed possible.
public sealed class TenantDirectory : ITenantDirectory
{
    private readonly TenantsDbContext _db;

    public TenantDirectory(TenantsDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<Guid, TenantSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> tenantIds, CancellationToken cancellationToken = default)
    {
        // Short-circuit the empty case: an IN () with no values is a pointless round-trip.
        if (tenantIds.Count == 0)
            return new Dictionary<Guid, TenantSummary>();

        var ids = tenantIds.Distinct().ToList();

        return await _db.Tenants
            .AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .ToDictionaryAsync(
                t => t.Id,
                t => new TenantSummary(t.Id, t.Name, t.Slug),
                cancellationToken);
    }

    public async Task<IReadOnlyCollection<Guid>> SearchIdsByNameAsync(
        string term, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(term))
            return Array.Empty<Guid>();

        // Provider-agnostic case-insensitive match, mirroring the Jobs title search: EF translates
        // this to lower(Name) LIKE '%term%' rather than depending on the Npgsql-specific ILike here.
        var normalized = term.Trim().ToLower();

        return await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Name.ToLower().Contains(normalized))
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<TenantPublicProfile?> GetPublicProfileBySlugAsync(
        string slug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return null;

        // Slugs are stored lower-cased (Tenant.Create normalizes), so match on the normalized form.
        var normalized = slug.Trim().ToLower();

        return await _db.Tenants
            .AsNoTracking()
            .Where(t => t.Slug == normalized)
            .Select(t => new TenantPublicProfile(
                t.Id, t.Name, t.Slug, t.Description, t.Website, t.Location))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<Guid>> GetTenantUserIdsAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        // ApplicationUser is not ITenantScoped (it is an Identity entity), so the tenant filter is
        // explicit here rather than coming from the global query filter — same reasoning as
        // AuthService.ListTenantUsersAsync.
        return await _db.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
    }
}
