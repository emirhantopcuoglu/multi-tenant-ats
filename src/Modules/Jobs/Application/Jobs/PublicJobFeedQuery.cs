using Ats.Modules.Jobs.Domain;
using Ats.Shared.Contracts.Tenants;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Jobs.Application.Jobs;

public sealed record PublicJobFeedItemDto(
    Guid Id, string Title, string CompanyName, string CompanySlug,
    string City, string? Country, string EmploymentType, string ExperienceLevel, string WorkArrangement,
    string Slug, DateTime? PublishedAtUtc);

// Enum filters arrive as strings; unknown values mean "no filter", not a 400.
public sealed record ListPublicJobFeedQuery(
    int Page = 1, int PageSize = 20, string? Search = null,
    string? EmploymentType = null, string? ExperienceLevel = null, string? WorkArrangement = null,
    string? Location = null)
    : IQuery<PagedResult<PublicJobFeedItemDto>>;

// The only query here that spans all tenants: it ignores the query filter and re-adds the
// soft-delete check by hand. Company names come from ITenantDirectory in two batched calls.
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
            // So "acme" matches job titles and the company Acme.
            var companyMatchIds = (await _tenants.SearchIdsByNameAsync(term, ct)).ToList();
            jobs = jobs.Where(j =>
                j.Title.ToLower().Contains(term) || companyMatchIds.Contains(j.TenantId));
        }

        // IsDefined because Enum.TryParse accepts numeric strings like "99".
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
            // Substring match on free-text city/country.
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

        var tenantIds = pageRows.Select(r => r.TenantId).Distinct().ToList();
        var companies = await _tenants.GetSummariesAsync(tenantIds, ct);

        var items = pageRows
            // No FK across schemas, so drop jobs whose tenant no longer exists.
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

    private sealed record PublicJobRow(
        Guid Id, string Title, Guid TenantId, string City, string? Country,
        string EmploymentType, string ExperienceLevel, string WorkArrangement,
        string Slug, DateTime? PublishedAtUtc);
}
