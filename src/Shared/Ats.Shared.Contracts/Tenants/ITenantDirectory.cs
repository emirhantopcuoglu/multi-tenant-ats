namespace Ats.Shared.Contracts.Tenants;

// The Tenants module's public read surface for other modules. Like IJobDirectory, this is a port:
// modules never reference each other's schema directly, they talk through a contract in this neutral
// shared assembly. The cross-tenant public job feed uses it to name the company behind each job
// (which lives in the Tenants schema) without the Jobs module reaching into it.
//
// Unlike most reads in the system, these methods are NOT tenant-scoped — the public marketplace spans
// every tenant. There is no global query filter on the Tenant entity (a tenant is the scope root, not
// a scoped row), so the implementation reads across all tenants by design.
public interface ITenantDirectory
{
    // Resolve display info for a batch of tenants in one query. Batched on purpose: naming the
    // companies behind a page of cross-tenant jobs one-by-one would be an N+1 (one extra query per
    // row). Missing ids are simply absent from the result; the caller decides how to handle a gap.
    Task<IReadOnlyDictionary<Guid, TenantSummary>> GetSummariesAsync(
        IReadOnlyCollection<Guid> tenantIds, CancellationToken cancellationToken = default);

    // Ids of tenants whose company name matches the search term. Lets a cross-tenant job search filter
    // by company name from the Jobs module without it querying (or knowing) the Tenants schema — the
    // Jobs handler ORs these ids into its own predicate.
    Task<IReadOnlyCollection<Guid>> SearchIdsByNameAsync(
        string term, CancellationToken cancellationToken = default);

    // Resolve a single company by its public URL slug, or null if no such tenant exists. Backs the
    // public company profile (/public/companies/{slug}): the Jobs module identifies the company here,
    // then counts its own published jobs, without reaching into the Tenants schema. Richer than
    // TenantSummary on purpose — the profile page shows fields the batched list reads never need.
    Task<TenantPublicProfile?> GetPublicProfileBySlugAsync(
        string slug, CancellationToken cancellationToken = default);

    // Ids of every user belonging to the given tenant. Backs the "new application" notification
    // fan-out: the Notifications module has no view onto ApplicationUser (an Identity entity in the
    // Tenants schema), so it asks here for the recipient list instead. Takes the tenant id explicitly
    // rather than reading ICurrentTenant — a message consumer has no ambient tenant context.
    Task<IReadOnlyCollection<Guid>> GetTenantUserIdsAsync(
        Guid tenantId, CancellationToken cancellationToken = default);
}

// A minimal read model — only the two fields a public listing needs to name and link a company.
// Deliberately not the Tenant entity: exposing the aggregate would leak the Tenants module's shape.
public sealed record TenantSummary(Guid Id, string CompanyName, string Slug);

// The single-company read model behind the public profile page. The optional fields are the
// tenant-editable profile; null means "never filled in" and the caller hides that section.
public sealed record TenantPublicProfile(
    Guid Id,
    string CompanyName,
    string Slug,
    string? Description,
    string? Website,
    string? Location);
