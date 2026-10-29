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

// Spans all tenants: the candidate account is the scope, not a tenant, so each queryable calls
// IgnoreQueryFilters. Company names and job titles come in two batched calls.
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

// Candidate-safe by shape: no field for the acting user or the internal rejection reason.
public sealed record CandidatePipelineStageDto(Guid Id, string Name, string Type, int Order);

public sealed record CandidateTimelineEntryDto(string Type, string? StageName, DateTime OccurredAtUtc);

// Its own record so a Shared.Contracts type isn't exposed directly. RoomToken is null for phone screens.
public sealed record CandidateInterviewDto(
    Guid Id, string Type, DateTime ScheduledAtUtc, int DurationMinutes, string Status,
    string? RoomToken);

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
        // Ownership is part of the WHERE, so someone else's id looks like a missing one.
        var application = await _db.Applications
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => !a.IsDeleted
                        && a.Id == query.ApplicationId
                        && a.CandidateAccountId == query.CandidateAccountId)
            .Select(a => new
            {
                a.Id,
                a.JobId,
                a.TenantId,
                a.CurrentStageId,
                a.Status,
                a.AppliedAtUtc,
                a.FirstViewedAtUtc
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

        // The tenant comes from the verified row, never from the caller.
        var entries = await _activityLog.GetByApplicationAsync(application.Id, application.TenantId, ct);
        var stageNames = stages.ToDictionary(s => s.Id, s => s.Name);
        var timeline = BuildCandidateTimeline(entries, stageNames);

        var interviews = await _interviews.GetForApplicationAsync(application.TenantId, application.Id, ct);
        var interviewDtos = interviews
            .Select(i => new CandidateInterviewDto(
                i.Id, i.Type, i.ScheduledAtUtc, i.DurationMinutes, i.Status, i.RoomToken))
            .ToList();

        return Result.Success(new CandidateApplicationDetailDto(
            application.Id, job.Title, job.Slug, company.CompanyName, company.Slug,
            application.Status.ToString(), application.AppliedAtUtc, application.FirstViewedAtUtc,
            application.CurrentStageId, stages, timeline, interviewDtos));
    }

    private async Task<IReadOnlyList<CandidatePipelineStageDto>> LoadPipelineStagesAsync(
        Guid jobId, Guid tenantId, CancellationToken ct)
    {
        // TenantId is matched explicitly because the filter is bypassed.
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

    // Oldest first, only the earliest Viewed, stage ids resolved to names. The rejection reason is
    // never read.
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
            // A malformed payload becomes a nameless move instead of a 500.
        }

        return null;
    }
}

public sealed record ListCandidateAppliedJobIdsQuery(Guid CandidateAccountId)
    : IQuery<IReadOnlyList<Guid>>;

// Only Active applications count, matching SubmitApplicationHandler's duplicate rule. Not paged:
// it is a small set the pages need in full.
public sealed class ListCandidateAppliedJobIdsHandler
    : IQueryHandler<ListCandidateAppliedJobIdsQuery, IReadOnlyList<Guid>>
{
    private readonly IApplicationsDbContext _db;

    public ListCandidateAppliedJobIdsHandler(IApplicationsDbContext db) => _db = db;

    public async Task<Result<IReadOnlyList<Guid>>> Handle(
        ListCandidateAppliedJobIdsQuery query, CancellationToken ct)
    {
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

// All of the candidate's interviews, across companies, for the "My interviews" tab.
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
