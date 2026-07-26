using System.Text.Json;
using Ats.Modules.Applications.Domain;
using Ats.Shared.Contracts.Interviews;
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

// ---- GetCandidateApplicationDetail ----
// The candidate's transparent view of one application: where it sits in the company's full
// pipeline plus a timeline of what happened. These DTOs are candidate-safe BY SHAPE — they have
// no field for the acting user or the internal rejection reason, so a mapping bug cannot leak
// either; the projection below never reads them into the response in the first place.
public sealed record CandidatePipelineStageDto(Guid Id, string Name, string Type, int Order);

public sealed record CandidateTimelineEntryDto(string Type, string? StageName, DateTime OccurredAtUtc);

// Candidate-safe projection of CandidateInterviewInfo — same shape, kept as its own record so this
// module's public contract doesn't leak a Shared.Contracts type verbatim into the API response.
public sealed record CandidateInterviewDto(
    Guid Id, string Type, DateTime ScheduledAtUtc, int DurationMinutes, string Status);

public sealed record CandidateApplicationDetailDto(
    Guid Id,
    string JobTitle,
    string JobSlug,
    string CompanyName,
    string CompanySlug,
    string Status,
    DateTime AppliedAtUtc,
    DateTime? FirstViewedAtUtc,
    Guid CurrentStageId,
    IReadOnlyList<CandidatePipelineStageDto> PipelineStages,
    IReadOnlyList<CandidateTimelineEntryDto> Timeline,
    IReadOnlyList<CandidateInterviewDto> Interviews);

public sealed record GetCandidateApplicationDetailQuery(Guid CandidateAccountId, Guid ApplicationId)
    : IQuery<CandidateApplicationDetailDto>;

public sealed class GetCandidateApplicationDetailHandler
    : IQueryHandler<GetCandidateApplicationDetailQuery, CandidateApplicationDetailDto>
{
    private readonly IApplicationsDbContext _db;
    private readonly IJobDirectory _jobs;
    private readonly ITenantDirectory _tenants;
    private readonly IActivityLogRepository _activityLog;
    private readonly IInterviewDirectory _interviews;

    public GetCandidateApplicationDetailHandler(
        IApplicationsDbContext db,
        IJobDirectory jobs,
        ITenantDirectory tenants,
        IActivityLogRepository activityLog,
        IInterviewDirectory interviews)
    {
        _db = db;
        _jobs = jobs;
        _tenants = tenants;
        _activityLog = activityLog;
        _interviews = interviews;
    }

    public async Task<Result<CandidateApplicationDetailDto>> Handle(
        GetCandidateApplicationDetailQuery query, CancellationToken ct)
    {
        // Ownership is part of the WHERE, not a separate check: an application that exists but
        // belongs to someone else is indistinguishable from one that doesn't exist. Probing ids
        // therefore reveals nothing. Cross-tenant scope as in the list query above.
        var application = await _db.Applications
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => !a.IsDeleted
                        && a.Id == query.ApplicationId
                        && a.CandidateAccountId == query.CandidateAccountId)
            .Select(a => new
            {
                a.Id, a.JobId, a.TenantId, a.CurrentStageId,
                a.Status, a.AppliedAtUtc, a.FirstViewedAtUtc
            })
            .FirstOrDefaultAsync(ct);

        if (application is null)
            return Result.Failure<CandidateApplicationDetailDto>(ApplicationErrors.NotFound);

        var jobs = await _jobs.GetSummariesAsync([application.JobId], ct);
        var companies = await _tenants.GetSummariesAsync([application.TenantId], ct);
        if (!jobs.TryGetValue(application.JobId, out var job)
            || !companies.TryGetValue(application.TenantId, out var company))
            return Result.Failure<CandidateApplicationDetailDto>(ApplicationErrors.NotFound);

        var stages = await LoadPipelineStagesAsync(application.JobId, application.TenantId, ct);
        if (stages.Count == 0)
            return Result.Failure<CandidateApplicationDetailDto>(ApplicationErrors.NotFound);

        // The tenant comes from the application row we just verified — never from the caller.
        var entries = await _activityLog.GetByApplicationAsync(application.Id, application.TenantId, ct);
        var stageNames = stages.ToDictionary(s => s.Id, s => s.Name);
        var timeline = BuildCandidateTimeline(entries, stageNames);

        var interviews = await _interviews.GetForApplicationAsync(application.TenantId, application.Id, ct);
        var interviewDtos = interviews
            .Select(i => new CandidateInterviewDto(
                i.Id, i.Type, i.ScheduledAtUtc, i.DurationMinutes, i.Status))
            .ToList();

        return Result.Success(new CandidateApplicationDetailDto(
            application.Id, job.Title, job.Slug, company.CompanyName, company.Slug,
            application.Status.ToString(), application.AppliedAtUtc, application.FirstViewedAtUtc,
            application.CurrentStageId, stages, timeline, interviewDtos));
    }

    private async Task<IReadOnlyList<CandidatePipelineStageDto>> LoadPipelineStagesAsync(
        Guid jobId, Guid tenantId, CancellationToken ct)
    {
        // Two narrow queries instead of a join: the pipeline id first, then its live stages in
        // funnel order. TenantId is matched explicitly because the global filter is bypassed.
        var pipelineId = await _db.Pipelines
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(p => !p.IsDeleted && p.JobId == jobId && p.TenantId == tenantId)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);

        if (pipelineId is null)
            return [];

        return await _db.PipelineStages
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => !s.IsDeleted && s.PipelineId == pipelineId)
            .OrderBy(s => s.Order)
            .Select(s => new CandidatePipelineStageDto(s.Id, s.Name, s.Type.ToString(), s.Order))
            .ToListAsync(ct);
    }

    // Pure mapping from internal log entries to the candidate-visible timeline. Ascending by
    // time; only the earliest Viewed survives (a concurrent double-stamp may log two); stage ids
    // resolve to names here so raw ids never reach the candidate; the Rejected payload's internal
    // reason is intentionally never read.
    internal static IReadOnlyList<CandidateTimelineEntryDto> BuildCandidateTimeline(
        IReadOnlyList<ActivityLogEntry> entries, IReadOnlyDictionary<Guid, string> stageNames)
    {
        var timeline = new List<CandidateTimelineEntryDto>();
        var viewedIncluded = false;

        foreach (var entry in entries.OrderBy(e => e.OccurredAtUtc))
        {
            switch (entry.ActivityType)
            {
                case nameof(ApplicationActivityType.Submitted):
                    timeline.Add(new(nameof(ApplicationActivityType.Submitted), null, entry.OccurredAtUtc));
                    break;
                case nameof(ApplicationActivityType.Viewed) when !viewedIncluded:
                    viewedIncluded = true;
                    timeline.Add(new(nameof(ApplicationActivityType.Viewed), null, entry.OccurredAtUtc));
                    break;
                case nameof(ApplicationActivityType.StageChanged):
                    timeline.Add(new(
                        nameof(ApplicationActivityType.StageChanged),
                        ResolveToStageName(entry.Payload, stageNames),
                        entry.OccurredAtUtc));
                    break;
                case nameof(ApplicationActivityType.Rejected):
                    timeline.Add(new(nameof(ApplicationActivityType.Rejected), null, entry.OccurredAtUtc));
                    break;
                case nameof(ApplicationActivityType.Hired):
                    timeline.Add(new(nameof(ApplicationActivityType.Hired), null, entry.OccurredAtUtc));
                    break;
                case nameof(ApplicationActivityType.Withdrawn):
                    timeline.Add(new(nameof(ApplicationActivityType.Withdrawn), null, entry.OccurredAtUtc));
                    break;
            }
        }

        return timeline;
    }

    private static string? ResolveToStageName(
        string payload, IReadOnlyDictionary<Guid, string> stageNames)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("toStageId", out var property)
                && Guid.TryParse(property.GetString(), out var stageId)
                && stageNames.TryGetValue(stageId, out var name))
                return name;
        }
        catch (JsonException)
        {
            // A legacy or malformed payload should degrade to a nameless move, not a 500.
        }

        return null;
    }
}

// ---- ListCandidateAppliedJobIds ----
public sealed record ListCandidateAppliedJobIdsQuery(Guid CandidateAccountId)
    : IQuery<IReadOnlyList<Guid>>;

// Returns the job ids the candidate currently has an Active application for, so the public job
// pages can render an "already applied" state instead of the apply CTA. Only Active applications
// count — SubmitApplicationHandler's duplicate rule allows re-applying after a rejection or
// withdrawal, and this projection must mirror that rule exactly or the UI will contradict the API.
//
// The result is deliberately unpaged: it is a membership set of GUIDs bounded by how many jobs one
// person can apply to, and the pages that consume it need the whole set for O(1) lookups.
public sealed class ListCandidateAppliedJobIdsHandler
    : IQueryHandler<ListCandidateAppliedJobIdsQuery, IReadOnlyList<Guid>>
{
    private readonly IApplicationsDbContext _db;

    public ListCandidateAppliedJobIdsHandler(IApplicationsDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<Guid>>> Handle(
        ListCandidateAppliedJobIdsQuery query, CancellationToken ct)
    {
        // Same cross-tenant scope as the list above: the global candidate account is the root,
        // so the tenant filter is explicitly bypassed.
        var jobIds = await _db.Applications
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => !a.IsDeleted
                        && a.CandidateAccountId == query.CandidateAccountId
                        && a.Status == ApplicationStatus.Active)
            .Select(a => a.JobId)
            .Distinct()
            .ToListAsync(ct);

        return Result.Success<IReadOnlyList<Guid>>(jobIds);
    }
}

// ---- ListCandidateInterviews ----
// The candidate's own interviews across every application they hold, regardless of which company
// or job it's under — the aggregate the "My interviews" tab needs, as opposed to the per-application
// list GetCandidateApplicationDetailQuery already returns.
public sealed record CandidateInterviewSummaryDto(
    Guid Id,
    Guid ApplicationId,
    string JobTitle,
    string CompanyName,
    string Type,
    DateTime ScheduledAtUtc,
    int DurationMinutes,
    string Status,
    string? RoomToken);

public sealed record ListCandidateInterviewsQuery(Guid CandidateAccountId) : IQuery<IReadOnlyList<CandidateInterviewSummaryDto>>;

public sealed class ListCandidateInterviewsHandler
    : IQueryHandler<ListCandidateInterviewsQuery, IReadOnlyList<CandidateInterviewSummaryDto>>
{
    private readonly IApplicationsDbContext _db;
    private readonly IJobDirectory _jobs;
    private readonly ITenantDirectory _tenants;
    private readonly IInterviewDirectory _interviews;

    public ListCandidateInterviewsHandler(
        IApplicationsDbContext db, IJobDirectory jobs, ITenantDirectory tenants, IInterviewDirectory interviews)
    {
        _db = db;
        _jobs = jobs;
        _tenants = tenants;
        _interviews = interviews;
    }

    public async Task<Result<IReadOnlyList<CandidateInterviewSummaryDto>>> Handle(
        ListCandidateInterviewsQuery query, CancellationToken ct)
    {
        // Same cross-tenant scope as ListCandidateApplicationsHandler: the global account is the
        // scope root, so every application this candidate holds is fair game regardless of tenant.
        var applications = await _db.Applications
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => !a.IsDeleted && a.CandidateAccountId == query.CandidateAccountId)
            .Select(a => new { a.Id, a.JobId, a.TenantId })
            .ToListAsync(ct);

        if (applications.Count == 0)
            return Result.Success<IReadOnlyList<CandidateInterviewSummaryDto>>([]);

        var applicationIds = applications.Select(a => a.Id).ToList();
        var interviews = await _interviews.GetForApplicationsAsync(applicationIds, ct);
        if (interviews.Count == 0)
            return Result.Success<IReadOnlyList<CandidateInterviewSummaryDto>>([]);

        var applicationsById = applications.ToDictionary(a => a.Id);
        var jobIds = applications.Select(a => a.JobId).Distinct().ToList();
        var jobs = await _jobs.GetSummariesAsync(jobIds, ct);
        var tenantIds = applications.Select(a => a.TenantId).Distinct().ToList();
        var companies = await _tenants.GetSummariesAsync(tenantIds, ct);

        var items = interviews
            .Where(i => applicationsById.ContainsKey(i.ApplicationId))
            .Select(i =>
            {
                var application = applicationsById[i.ApplicationId];
                jobs.TryGetValue(application.JobId, out var job);
                companies.TryGetValue(application.TenantId, out var company);
                return new CandidateInterviewSummaryDto(
                    i.Id, i.ApplicationId, job?.Title ?? string.Empty, company?.CompanyName ?? string.Empty,
                    i.Type, i.ScheduledAtUtc, i.DurationMinutes, i.Status, i.RoomToken);
            })
            .OrderByDescending(i => i.ScheduledAtUtc)
            .ToList();

        return Result.Success<IReadOnlyList<CandidateInterviewSummaryDto>>(items);
    }
}
