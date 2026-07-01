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
}

// A minimal read model — only the two fields a public listing needs to name and link a company.
// Deliberately not the Tenant entity: exposing the aggregate would leak the Tenants module's shape.
public sealed record TenantSummary(Guid Id, string CompanyName, string Slug);
