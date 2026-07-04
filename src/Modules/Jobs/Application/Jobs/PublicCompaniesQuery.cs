using Ats.Modules.Jobs.Domain;
using Ats.Shared.Contracts.Tenants;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Jobs.Application.Jobs;

// A company as the public marketplace sees it: a name to show, a slug to link to its careers page
// (/{slug}), and how many roles it is currently hiring for. "Company" here is defined by having
// Published jobs — this is the public hiring directory, not the tenant admin list.
public sealed record PublicCompanyDto(string CompanyName, string Slug, int OpenJobCount);

// ---- ListPublicCompanies ----
// The marketplace "browse companies" view: every company with at least one Published job, most-hiring
// first, with an optional search over company name. Like the job feed, this deliberately spans all
// tenants (IgnoreQueryFilters) — a company that is hiring is public across the whole marketplace.
//
// Why this lives in the Jobs module: the set of "companies hiring" and their open-role counts are
// derived entirely from Published jobs, which only this module can see. The company name/slug are the
// Tenants module's data, read through the ITenantDirectory port (batched, no N+1) rather than by
// touching its schema — the same split as the cross-tenant job feed.
public sealed record ListPublicCompaniesQuery(int Page = 1, int PageSize = 20, string? Search = null)
    : IQuery<PagedResult<PublicCompanyDto>>;

public sealed class ListPublicCompaniesHandler
    : IQueryHandler<ListPublicCompaniesQuery, PagedResult<PublicCompanyDto>>
{
    private readonly IJobsDbContext _db;
    private readonly ITenantDirectory _tenants;

    public ListPublicCompaniesHandler(IJobsDbContext db, ITenantDirectory tenants)
    {
        _db = db;
        _tenants = tenants;
    }

    public async Task<Result<PagedResult<PublicCompanyDto>>> Handle(
        ListPublicCompaniesQuery query, CancellationToken ct)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var published = _db.Jobs
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(j => !j.IsDeleted && j.Status == JobStatus.Published);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Company-name search happens in the Tenants module; restrict the grouping to the matching
            // tenants so a company with no name match never appears, even if it has published jobs.
            var term = query.Search.Trim().ToLower();
            var nameMatchIds = (await _tenants.SearchIdsByNameAsync(term, ct)).ToList();
            published = published.Where(j => nameMatchIds.Contains(j.TenantId));
        }

        // One row per company: the tenant id and its open-role count. Ordered most-hiring first; the
        // tenant id is a stable tiebreaker so paging is deterministic (name would order nicer, but it
        // lives in the other module — a per-page name sort is applied after enrichment below).
        var grouped = published
            .GroupBy(j => j.TenantId)
            .Select(g => new { TenantId = g.Key, OpenJobCount = g.Count() });

        var totalCount = await grouped.CountAsync(ct);

        var pageRows = await grouped
            .OrderByDescending(x => x.OpenJobCount)
            .ThenBy(x => x.TenantId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var companies = await _tenants.GetSummariesAsync(
            pageRows.Select(r => r.TenantId).ToList(), ct);

        var items = pageRows
            .Where(r => companies.ContainsKey(r.TenantId))
            .Select(r => new PublicCompanyDto(
                companies[r.TenantId].CompanyName, companies[r.TenantId].Slug, r.OpenJobCount))
            .ToList();

        return Result.Success(new PagedResult<PublicCompanyDto>(items, page, pageSize, totalCount));
    }
}

// ---- GetPublicCompanyBySlug ----
// A single company's public profile, backing the careers page header at /{slug}. The company is
// identified by slug through the Tenants port, then this module counts its published jobs. Returns a
// failure (→ 404) when no tenant owns the slug; a real company with zero open roles still resolves,
// matching the careers page, which renders an empty-but-valid listing.
//
// A separate DTO from PublicCompanyDto: the directory list stays lean (name, slug, count) while the
// profile page carries the tenant-editable fields. Null profile fields mean "never filled in" — the
// page hides those sections rather than rendering empty blocks.
public sealed record PublicCompanyProfileDto(
    string CompanyName,
    string Slug,
    string? Description,
    string? Website,
    string? Location,
    int OpenJobCount);

public sealed record GetPublicCompanyBySlugQuery(string Slug) : IQuery<PublicCompanyProfileDto>;

public sealed class GetPublicCompanyBySlugHandler
    : IQueryHandler<GetPublicCompanyBySlugQuery, PublicCompanyProfileDto>
{
    private readonly IJobsDbContext _db;
    private readonly ITenantDirectory _tenants;

    public GetPublicCompanyBySlugHandler(IJobsDbContext db, ITenantDirectory tenants)
    {
        _db = db;
        _tenants = tenants;
    }

    public async Task<Result<PublicCompanyProfileDto>> Handle(
        GetPublicCompanyBySlugQuery query, CancellationToken ct)
    {
        var company = await _tenants.GetPublicProfileBySlugAsync(query.Slug, ct);
        if (company is null)
            return Result.Failure<PublicCompanyProfileDto>(CompanyErrors.NotFound);

        var openJobCount = await _db.Jobs
            .AsNoTracking()
            .IgnoreQueryFilters()
            .CountAsync(j => !j.IsDeleted && j.TenantId == company.Id && j.Status == JobStatus.Published, ct);

        return Result.Success(new PublicCompanyProfileDto(
            company.CompanyName, company.Slug,
            company.Description, company.Website, company.Location,
            openJobCount));
    }
}

public static class CompanyErrors
{
    public static readonly Error NotFound =
        new("company.not_found", "No company exists at this address.");
}
