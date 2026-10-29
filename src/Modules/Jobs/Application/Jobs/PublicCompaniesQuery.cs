using Ats.Modules.Jobs.Domain;
using Ats.Shared.Contracts.Tenants;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Jobs.Application.Jobs;

// A company with at least one published job.
public sealed record PublicCompanyDto(string CompanyName, string Slug, int OpenJobCount);

// Companies with published jobs, most open roles first, across all tenants.
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
            // Name search runs in the Tenants module; only matching tenants are grouped.
            var term = query.Search.Trim().ToLower();
            var nameMatchIds = (await _tenants.SearchIdsByNameAsync(term, ct)).ToList();
            published = published.Where(j => nameMatchIds.Contains(j.TenantId));
        }

        // Tenant id is the tiebreaker so paging is stable; names live in the other module.
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

// Resolves for any existing tenant, even one with no open roles, to match the careers page.
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
