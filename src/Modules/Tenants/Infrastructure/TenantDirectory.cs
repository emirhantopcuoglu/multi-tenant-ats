using Ats.Shared.Contracts.Tenants;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Tenants.Infrastructure;

// Reads span all tenants (Tenant has no query filter); the public feed needs that.
public sealed class TenantDirectory : ITenantDirectory
{
    private readonly TenantsDbContext _db;

    public TenantDirectory(TenantsDbContext db) => _db = db;

    public async Task<IReadOnlyDictionary<Guid, TenantSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> tenantIds, CancellationToken cancellationToken = default)
    {
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

        // Translates to lower(Name) LIKE, same as the job title search.
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
        // ApplicationUser isn't ITenantScoped, so the tenant filter is explicit.
        return await _db.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
    }
}
