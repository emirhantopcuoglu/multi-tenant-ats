using Ats.Modules.Jobs.Domain;
using Ats.Shared.Contracts.Tenants;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Jobs.Application.Jobs;

// One card in the cross-tenant marketplace feed. It carries the company name + slug (unlike the
// tenant-scoped JobDto, which never needs them — the tenant is implied by the URL there). The slug
// pair lets the frontend link to the existing careers detail page at /{companySlug}/jobs/{slug}.
public sealed record PublicJobFeedItemDto(
    Guid Id, string Title, string CompanyName, string CompanySlug,
    string City, string? Country, string EmploymentType, string ExperienceLevel, string WorkArrangement,
    string Slug, DateTime? PublishedAtUtc);

// The public marketplace feed: every tenant's Published jobs in one list, newest first, with an
// optional search over job title OR company name, plus optional narrowing filters. The enum
// filters travel as strings because they come straight off a public query string: an unknown
// value must degrade to "no filter", not to a 400 — these URLs are shared and hand-edited, and a
// broken filter should never break the page.
public sealed record ListPublicJobFeedQuery(
    int Page = 1, int PageSize = 20, string? Search = null,
    string? EmploymentType = null, string? ExperienceLevel = null, string? WorkArrangement = null,
    string? Location = null)
    : IQuery<PagedResult<PublicJobFeedItemDto>>;

// Unlike every other query in this module, this one deliberately spans all tenants. The tenant
// global query filter is the system's default isolation boundary; "a published job is public across
// the whole marketplace" is the one intentional exception to it, so the handler calls
// IgnoreQueryFilters() and re-adds by hand the soft-delete predicate that filter also carried.
//
// The company name/slug live in the Tenants schema, which this module must not touch directly. It
// reads them through ITenantDirectory (a cross-module port) in two batched calls — one to translate a
// company-name search into tenant ids, one to name the companies on the resulting page — so the cost
// stays constant regardless of page size (no N+1).
public sealed class ListPublicJobFeedHandler
    : IQueryHandler<ListPublicJobFeedQuery, PagedResult<PublicJobFeedItemDto>>
{
    private readonly IJobsDbContext _db;
    private readonly ITenantDirectory _tenants;

    public ListPublicJobFeedHandler(IJobsDbContext db, ITenantDirectory tenants)
    {
        _db = db;
        _tenants = tenants;
    }

    public async Task<Result<PagedResult<PublicJobFeedItemDto>>> Handle(
        ListPublicJobFeedQuery query, CancellationToken ct)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var jobs = _db.Jobs
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(j => !j.IsDeleted && j.Status == JobStatus.Published);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            // Resolve which companies match by name first, then OR them into the job predicate so a
            // search for "acme" finds both jobs titled *acme* and jobs at the company *Acme*.
            var companyMatchIds = (await _tenants.SearchIdsByNameAsync(term, ct)).ToList();
            jobs = jobs.Where(j =>
                j.Title.ToLower().Contains(term) || companyMatchIds.Contains(j.TenantId));
        }

        // IsDefined guards against Enum.TryParse's numeric-string quirk: "99" parses "successfully"
        // into an undefined value, which would silently filter everything out.
        if (Enum.TryParse<EmploymentType>(query.EmploymentType, ignoreCase: true, out var employmentType)
            && Enum.IsDefined(employmentType))
        {
            jobs = jobs.Where(j => j.EmploymentType == employmentType);
        }

        if (Enum.TryParse<ExperienceLevel>(query.ExperienceLevel, ignoreCase: true, out var experienceLevel)
            && Enum.IsDefined(experienceLevel))
        {
            jobs = jobs.Where(j => j.ExperienceLevel == experienceLevel);
        }

        if (Enum.TryParse<WorkArrangement>(query.WorkArrangement, ignoreCase: true, out var workArrangement)
            && Enum.IsDefined(workArrangement))
        {
            jobs = jobs.Where(j => j.WorkArrangement == workArrangement);
        }

        if (!string.IsNullOrWhiteSpace(query.Location))
        {
            // City/Country are free text, so a substring match ("istanbul" → city "Istanbul",
            // "turkey" → country "Turkey") is the honest granularity — no location taxonomy exists yet.
            var location = query.Location.Trim().ToLower();
            jobs = jobs.Where(j =>
                j.City.ToLower().Contains(location) ||
                (j.Country != null && j.Country.ToLower().Contains(location)));
        }

        var totalCount = await jobs.CountAsync(ct);

        var pageRows = await jobs
            .OrderByDescending(j => j.PublishedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new PublicJobRow(
                j.Id, j.Title, j.TenantId, j.City, j.Country,
                j.EmploymentType.ToString(), j.ExperienceLevel.ToString(), j.WorkArrangement.ToString(),
                j.Slug, j.PublishedAtUtc))
            .ToListAsync(ct);

        // One batched lookup names every company on this page; a per-row lookup would be an N+1.
        var tenantIds = pageRows.Select(r => r.TenantId).Distinct().ToList();
        var companies = await _tenants.GetSummariesAsync(tenantIds, ct);

        var items = pageRows
            // A job whose tenant vanished (no cross-schema FK enforces this) is dropped rather than
            // rendered nameless — defensive, and in practice never hit.
            .Where(r => companies.ContainsKey(r.TenantId))
            .Select(r =>
            {
                var company = companies[r.TenantId];
                return new PublicJobFeedItemDto(
                    r.Id, r.Title, company.CompanyName, company.Slug,
                    r.City, r.Country, r.EmploymentType, r.ExperienceLevel, r.WorkArrangement,
                    r.Slug, r.PublishedAtUtc);
            })
            .ToList();

        return Result.Success(
            new PagedResult<PublicJobFeedItemDto>(items, page, pageSize, totalCount));
    }

    // Intermediate projection: the job columns pulled from the database before the company name is
    // stitched in from the Tenants module. Kept private — it is a step, not part of the contract.
    private sealed record PublicJobRow(
        Guid Id, string Title, Guid TenantId, string City, string? Country,
        string EmploymentType, string ExperienceLevel, string WorkArrangement,
        string Slug, DateTime? PublishedAtUtc);
}
