using Ats.Modules.Jobs.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Jobs.Application.Jobs;

public sealed record JobDto(
    Guid Id, string Title, string Department, string Location,
    string EmploymentType, string ExperienceLevel, string Status,
    string Slug, DateTime CreatedAtUtc);

// Detail shape for GetById: adds the fields the edit form must prefill (Description + salary), which
// the list DTO deliberately omits. Kept separate so list/public projections stay lean (the table
// never needs the description), while the edit screen gets everything it writes back.
// PublishedAtUtc is nullable because drafts have never been published; the public page shows it
// as the posting date (CreatedAtUtc is the draft's birth date and would mislead the public).
public sealed record JobDetailDto(
    Guid Id, string Title, string Description, string Department, string Location,
    string EmploymentType, string ExperienceLevel, string Status, string Slug,
    decimal? SalaryMin, decimal? SalaryMax, string? SalaryCurrency, DateTime CreatedAtUtc,
    DateTime? PublishedAtUtc);

// ---- GetJobById ----
public sealed record GetJobByIdQuery(Guid JobId) : IQuery<JobDetailDto>;

public sealed class GetJobByIdHandler : IQueryHandler<GetJobByIdQuery, JobDetailDto>
{
    private readonly IJobsDbContext _db;
    public GetJobByIdHandler(IJobsDbContext db) => _db = db;

    public async Task<Result<JobDetailDto>> Handle(GetJobByIdQuery query, CancellationToken ct)
    {
        // SalaryRange is an optional owned type, so all three columns are null together; the
        // null check projects them as null rather than dereferencing a missing dependent.
        var job = await _db.Jobs
            .AsNoTracking()
            .Where(j => j.Id == query.JobId)
            .Select(j => new JobDetailDto(
                j.Id, j.Title, j.Description, j.Department, j.Location,
                j.EmploymentType.ToString(), j.ExperienceLevel.ToString(), j.Status.ToString(), j.Slug,
                j.SalaryRange == null ? null : (decimal?)j.SalaryRange.Min,
                j.SalaryRange == null ? null : (decimal?)j.SalaryRange.Max,
                j.SalaryRange == null ? null : j.SalaryRange.Currency,
                j.CreatedAtUtc, j.PublishedAtUtc))
            .FirstOrDefaultAsync(ct);

        return job is null
            ? Result.Failure<JobDetailDto>(JobErrors.NotFound)
            : Result.Success(job);
    }
}

// ---- ListJobsForRecruiter ----
public sealed record ListJobsQuery(
    int Page = 1, int PageSize = 20, string? Status = null, string? Search = null)
    : IQuery<PagedResult<JobDto>>;

public sealed class ListJobsHandler : IQueryHandler<ListJobsQuery, PagedResult<JobDto>>
{
    private readonly IJobsDbContext _db;
    public ListJobsHandler(IJobsDbContext db) => _db = db;

    public async Task<Result<PagedResult<JobDto>>> Handle(ListJobsQuery query, CancellationToken ct)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var jobs = _db.Jobs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<JobStatus>(query.Status, true, out var status))
            jobs = jobs.Where(j => j.Status == status);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            // Provider-agnostic case-insensitive match; EF translates this to lower(Title) LIKE '%term%'.
            // Avoids depending on the Npgsql-specific EF.Functions.ILike from the Application layer.
            var term = query.Search.Trim().ToLower();
            jobs = jobs.Where(j => j.Title.ToLower().Contains(term));
        }

        var totalCount = await jobs.CountAsync(ct);

        var items = await jobs
            .OrderByDescending(j => j.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new JobDto(
                j.Id, j.Title, j.Department, j.Location,
                j.EmploymentType.ToString(), j.ExperienceLevel.ToString(), j.Status.ToString(),
                j.Slug, j.CreatedAtUtc))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<JobDto>(items, page, pageSize, totalCount));
    }
}

// ---- ListPublicJobs (no auth, only Published) ----
// Unlike the recruiter query, status is fixed to Published — the public never sees
// drafts. Tenant scoping is still automatic via the global query filter (no explicit
// WHERE TenantId here); EF appends it from the resolved tenant context.
public sealed record ListPublicJobsQuery(int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<JobDto>>;

public sealed class ListPublicJobsHandler : IQueryHandler<ListPublicJobsQuery, PagedResult<JobDto>>
{
    private readonly IJobsDbContext _db;
    public ListPublicJobsHandler(IJobsDbContext db) => _db = db;

    public async Task<Result<PagedResult<JobDto>>> Handle(ListPublicJobsQuery query, CancellationToken ct)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var jobs = _db.Jobs
            .AsNoTracking()
            .Where(j => j.Status == JobStatus.Published);

        var totalCount = await jobs.CountAsync(ct);

        var items = await jobs
            .OrderByDescending(j => j.PublishedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new JobDto(
                j.Id, j.Title, j.Department, j.Location,
                j.EmploymentType.ToString(), j.ExperienceLevel.ToString(), j.Status.ToString(),
                j.Slug, j.CreatedAtUtc))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<JobDto>(items, page, pageSize, totalCount));
    }
}

// ---- GetPublicJobBySlug (no auth, only Published) ----
// The public job-detail page looks a job up by its slug, not its Guid — the slug is what appears in
// the URL (/{tenant}/jobs/{jobSlug}). Only Published jobs are visible: a draft or closed job must
// read as "not found" to the public, never leak. Tenant scoping is automatic via the global query
// filter, so a slug from another tenant can't match.
public sealed record GetPublicJobBySlugQuery(string Slug) : IQuery<JobDetailDto>;

public sealed class GetPublicJobBySlugHandler : IQueryHandler<GetPublicJobBySlugQuery, JobDetailDto>
{
    private readonly IJobsDbContext _db;
    public GetPublicJobBySlugHandler(IJobsDbContext db) => _db = db;

    public async Task<Result<JobDetailDto>> Handle(GetPublicJobBySlugQuery query, CancellationToken ct)
    {
        var job = await _db.Jobs
            .AsNoTracking()
            .Where(j => j.Slug == query.Slug && j.Status == JobStatus.Published)
            .Select(j => new JobDetailDto(
                j.Id, j.Title, j.Description, j.Department, j.Location,
                j.EmploymentType.ToString(), j.ExperienceLevel.ToString(), j.Status.ToString(), j.Slug,
                j.SalaryRange == null ? null : (decimal?)j.SalaryRange.Min,
                j.SalaryRange == null ? null : (decimal?)j.SalaryRange.Max,
                j.SalaryRange == null ? null : j.SalaryRange.Currency,
                j.CreatedAtUtc, j.PublishedAtUtc))
            .FirstOrDefaultAsync(ct);

        return job is null
            ? Result.Failure<JobDetailDto>(JobErrors.NotFound)
            : Result.Success(job);
    }
}
