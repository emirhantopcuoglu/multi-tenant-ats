using System.Text.Json;
using Ats.Modules.Applications.Domain;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Applications.Application.Applications;

public sealed record ApplicationListItemDto(
    Guid Id, string CandidateName, string CandidateEmail,
    Guid JobId, Guid StageId, string StageName, string Status, DateTime AppliedAtUtc);

public sealed record ApplicationDetailDto(
    Guid Id, Guid JobId, Guid CandidateId, string CandidateName, string CandidateEmail,
    string? Phone, string? LinkedInUrl, Guid StageId, string StageName, string Status,
    string? CoverLetter, string? RejectionReason, bool HasCv, DateTime AppliedAtUtc);

public sealed record CvDownloadUrlDto(string Url, int ExpiresInSeconds);

public sealed record ListApplicationsQuery(
    Guid? JobId = null, Guid? StageId = null, string? Status = null, string? Search = null,
    int Page = 1, int PageSize = 20) : IQuery<PagedResult<ApplicationListItemDto>>;

public sealed class ListApplicationsHandler
    : IQueryHandler<ListApplicationsQuery, PagedResult<ApplicationListItemDto>>
{
    private readonly IApplicationsDbContext _db;
    public ListApplicationsHandler(IApplicationsDbContext db) => _db = db;

    public async Task<Result<PagedResult<ApplicationListItemDto>>> Handle(
        ListApplicationsQuery query, CancellationToken ct)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var applications = _db.Applications.AsNoTracking();
        if (query.JobId.HasValue)
            applications = applications.Where(a => a.JobId == query.JobId.Value);
        if (query.StageId.HasValue)
            applications = applications.Where(a => a.CurrentStageId == query.StageId.Value);
        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<ApplicationStatus>(query.Status, true, out var status))
            applications = applications.Where(a => a.Status == status);

        // Candidate and stage are separate aggregates referenced by id, so this is an explicit join.
        var joined =
            from a in applications
            join c in _db.Candidates.AsNoTracking() on a.CandidateId equals c.Id
            join s in _db.PipelineStages.AsNoTracking() on a.CurrentStageId equals s.Id
            select new { a, c, s };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            joined = joined.Where(x =>
                x.c.FirstName.ToLower().Contains(term)
                || x.c.LastName.ToLower().Contains(term)
                || x.c.Email.ToLower().Contains(term));
        }

        var totalCount = await joined.CountAsync(ct);

        var items = await joined
            .OrderByDescending(x => x.a.AppliedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ApplicationListItemDto(
                x.a.Id, x.c.FirstName + " " + x.c.LastName, x.c.Email,
                x.a.JobId, x.a.CurrentStageId, x.s.Name, x.a.Status.ToString(), x.a.AppliedAtUtc))
            .ToListAsync(ct);

        return Result.Success(new PagedResult<ApplicationListItemDto>(items, page, pageSize, totalCount));
    }
}

public sealed record GetApplicationByIdQuery(Guid Id) : IQuery<ApplicationDetailDto>;

public sealed class GetApplicationByIdHandler
    : IQueryHandler<GetApplicationByIdQuery, ApplicationDetailDto>
{
    private readonly IApplicationsDbContext _db;
    public GetApplicationByIdHandler(IApplicationsDbContext db) => _db = db;

    public async Task<Result<ApplicationDetailDto>> Handle(GetApplicationByIdQuery query, CancellationToken ct)
    {
        var detail = await (
            from a in _db.Applications.AsNoTracking()
            join c in _db.Candidates.AsNoTracking() on a.CandidateId equals c.Id
            join s in _db.PipelineStages.AsNoTracking() on a.CurrentStageId equals s.Id
            where a.Id == query.Id
            select new ApplicationDetailDto(
                a.Id, a.JobId, c.Id, c.FirstName + " " + c.LastName, c.Email,
                c.Phone, c.LinkedInUrl, a.CurrentStageId, s.Name, a.Status.ToString(),
                a.CoverLetter, a.RejectionReason, a.CvFileKey != null, a.AppliedAtUtc))
            .FirstOrDefaultAsync(ct);

        return detail is null
            ? Result.Failure<ApplicationDetailDto>(ApplicationErrors.NotFound)
            : Result.Success(detail);
    }
}

public sealed record GetCvDownloadUrlQuery(Guid Id) : IQuery<CvDownloadUrlDto>;

public sealed class GetCvDownloadUrlHandler : IQueryHandler<GetCvDownloadUrlQuery, CvDownloadUrlDto>
{
    private static readonly TimeSpan Expiry = TimeSpan.FromMinutes(5);

    private readonly IApplicationsDbContext _db;
    private readonly IFileStorage _fileStorage;

    public GetCvDownloadUrlHandler(IApplicationsDbContext db, IFileStorage fileStorage)
    {
        _db = db;
        _fileStorage = fileStorage;
    }

    public async Task<Result<CvDownloadUrlDto>> Handle(GetCvDownloadUrlQuery query, CancellationToken ct)
    {
        var cvFileKey = await _db.Applications.AsNoTracking()
            .Where(a => a.Id == query.Id)
            .Select(a => a.CvFileKey)
            .FirstOrDefaultAsync(ct);

        if (cvFileKey is null)
            return Result.Failure<CvDownloadUrlDto>(ApplicationErrors.NotFound);

        var url = await _fileStorage.GetPresignedDownloadUrlAsync(cvFileKey, Expiry, ct);
        return Result.Success(new CvDownloadUrlDto(url, (int)Expiry.TotalSeconds));
    }
}

// Payload is parsed so the response embeds a real JSON object, not an escaped string.
public sealed record ApplicationActivityDto(
    Guid Id, string ActivityType, Guid? ActorUserId, JsonElement Payload, DateTime OccurredAtUtc);

public sealed record GetApplicationActivityQuery(Guid ApplicationId)
    : IQuery<IReadOnlyList<ApplicationActivityDto>>;

public sealed class GetApplicationActivityHandler
    : IQueryHandler<GetApplicationActivityQuery, IReadOnlyList<ApplicationActivityDto>>
{
    private readonly IApplicationsDbContext _db;
    private readonly IActivityLogRepository _activityLog;

    public GetApplicationActivityHandler(IApplicationsDbContext db, IActivityLogRepository activityLog)
    {
        _db = db;
        _activityLog = activityLog;
    }

    public async Task<Result<IReadOnlyList<ApplicationActivityDto>>> Handle(
        GetApplicationActivityQuery query, CancellationToken ct)
    {
        // Check the application exists in this tenant first, so an unknown id is a 404.
        var exists = await _db.Applications.AsNoTracking()
            .AnyAsync(a => a.Id == query.ApplicationId, ct);
        if (!exists)
            return Result.Failure<IReadOnlyList<ApplicationActivityDto>>(ApplicationErrors.NotFound);

        var entries = await _activityLog.GetByApplicationAsync(query.ApplicationId, ct);

        var items = entries
            .Select(e => new ApplicationActivityDto(
                e.Id, e.ActivityType, e.ActorUserId,
                JsonSerializer.Deserialize<JsonElement>(e.Payload), e.OccurredAtUtc))
            .ToList();

        return Result.Success<IReadOnlyList<ApplicationActivityDto>>(items);
    }
}
