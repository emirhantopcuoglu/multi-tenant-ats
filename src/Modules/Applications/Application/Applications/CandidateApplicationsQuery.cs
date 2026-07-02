using Ats.Shared.Contracts.Jobs;
using Ats.Shared.Contracts.Tenants;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Applications.Application.Applications;

public sealed record CandidateApplicationSummaryDto(
    Guid Id,
    string JobTitle,
    string CompanyName,
    string CompanySlug,
    string JobSlug,
    DateTime AppliedAtUtc,
    string Status,
    string? CurrentStageName);

public sealed record ListCandidateApplicationsQuery(Guid CandidateAccountId, int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<CandidateApplicationSummaryDto>>;

// Reads a candidate's own applications across all tenants. Unlike every other query in this
// module, this one deliberately bypasses the tenant global filter — the global account is the
// scope root, not a tenant. IgnoreQueryFilters() is called explicitly on each queryable that
// carries the filter so the intent is unmissable at the call sites below.
//
// Company names (ITenantDirectory) and job titles/slugs (IJobDirectory) are fetched in two
// batched calls so the page cost is O(1), not O(n) per row.
public sealed class ListCandidateApplicationsHandler
    : IQueryHandler<ListCandidateApplicationsQuery, PagedResult<CandidateApplicationSummaryDto>>
{
    private readonly IApplicationsDbContext _db;
    private readonly IJobDirectory _jobs;
    private readonly ITenantDirectory _tenants;

    public ListCandidateApplicationsHandler(
        IApplicationsDbContext db, IJobDirectory jobs, ITenantDirectory tenants)
    {
        _db = db;
        _jobs = jobs;
        _tenants = tenants;
    }

    public async Task<Result<PagedResult<CandidateApplicationSummaryDto>>> Handle(
        ListCandidateApplicationsQuery query, CancellationToken ct)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var baseQuery = _db.Applications
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => !a.IsDeleted && a.CandidateAccountId == query.CandidateAccountId);

        var total = await baseQuery.CountAsync(ct);

        var rows = await baseQuery
            .OrderByDescending(a => a.AppliedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new { a.Id, a.JobId, a.TenantId, a.CurrentStageId, a.Status, a.AppliedAtUtc })
            .ToListAsync(ct);

        if (rows.Count == 0)
            return Result.Success(new PagedResult<CandidateApplicationSummaryDto>([], page, pageSize, total));

        // Batch the cross-module lookups so each page costs one extra query per port, not one per row.
        var stageIds = rows.Select(r => r.CurrentStageId).Distinct().ToList();
        var stages = await _db.PipelineStages
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => stageIds.Contains(s.Id) && !s.IsDeleted)
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        var jobIds = rows.Select(r => r.JobId).Distinct().ToList();
        var jobs = await _jobs.GetSummariesAsync(jobIds, ct);

        var tenantIds = rows.Select(r => r.TenantId).Distinct().ToList();
        var companies = await _tenants.GetSummariesAsync(tenantIds, ct);

        var items = rows
            .Where(r => jobs.ContainsKey(r.JobId) && companies.ContainsKey(r.TenantId))
            .Select(r =>
            {
                var job = jobs[r.JobId];
                var company = companies[r.TenantId];
                stages.TryGetValue(r.CurrentStageId, out var stageName);
                return new CandidateApplicationSummaryDto(
                    r.Id, job.Title, company.CompanyName, company.Slug,
                    job.Slug, r.AppliedAtUtc, r.Status.ToString(), stageName);
            })
            .ToList();

        return Result.Success(new PagedResult<CandidateApplicationSummaryDto>(items, page, pageSize, total));
    }
}
