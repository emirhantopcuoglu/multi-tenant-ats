namespace Ats.Shared.Contracts.Tenants;

// Read port into the Tenants module. Not tenant-scoped: the Tenant entity has no query filter
// and the public marketplace spans all tenants.
public interface ITenantDirectory
{
    // Batched to avoid one query per row. Missing ids are left out.
    Task<IReadOnlyDictionary<Guid, TenantSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> tenantIds, CancellationToken cancellationToken = default);

    // Lets the Jobs search match company names without knowing the Tenants schema.
    Task<IReadOnlyCollection<Guid>> SearchIdsByNameAsync(
        string term, CancellationToken cancellationToken = default);

    // Null if no tenant has this slug.
    Task<TenantPublicProfile?> GetPublicProfileBySlugAsync(
        string slug, CancellationToken cancellationToken = default);

    // For the new-application fan-out. Takes the tenant explicitly: consumers have no ambient tenant.
    Task<IReadOnlyCollection<Guid>> GetTenantUserIdsAsync(
        Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed record TenantSummary(Guid Id, string CompanyName, string Slug);

// Null fields were never filled in; the page hides them.
public sealed record TenantPublicProfile(
    Guid Id,
    string CompanyName,
    string Slug,
    string? Description,
    string? Website,
    string? Location);
