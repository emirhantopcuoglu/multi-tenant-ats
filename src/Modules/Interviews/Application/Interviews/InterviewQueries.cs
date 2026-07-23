using Ats.Shared.Contracts.Applications;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Interviews.Application.Interviews;

public sealed record InterviewListItemDto(
    Guid Id, Guid ApplicationId, string CandidateName, string Type, DateTime ScheduledAtUtc,
    int DurationMinutes, string Status, IReadOnlyList<Guid> InterviewerUserIds);

public sealed record InterviewDetailDto(
    Guid Id, Guid ApplicationId, string Type, DateTime ScheduledAtUtc, int DurationMinutes,
    string Status, string? Notes, IReadOnlyList<Guid> InterviewerUserIds, string RoomToken);

// ---- ListInterviews (filtered by date range / interviewer, paginated) ----
public sealed record ListInterviewsQuery(
    DateTime? FromDate = null, DateTime? ToDate = null, Guid? InterviewerId = null,
    Guid? ApplicationId = null, int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<InterviewListItemDto>>;

public sealed class ListInterviewsHandler
    : IQueryHandler<ListInterviewsQuery, PagedResult<InterviewListItemDto>>
{
    private readonly IInterviewsDbContext _db;
    private readonly IApplicationDirectory _applications;

    public ListInterviewsHandler(IInterviewsDbContext db, IApplicationDirectory applications)
    {
        _db = db;
        _applications = applications;
    }

    public async Task<Result<PagedResult<InterviewListItemDto>>> Handle(
        ListInterviewsQuery query, CancellationToken ct)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var interviews = _db.Interviews.AsNoTracking();
        if (query.FromDate.HasValue)
            interviews = interviews.Where(i => i.ScheduledAtUtc >= query.FromDate.Value);
        if (query.ToDate.HasValue)
            interviews = interviews.Where(i => i.ScheduledAtUtc <= query.ToDate.Value);
        // Translates to "@interviewerId = ANY(InterviewerUserIds)" against the uuid[] column.
        if (query.InterviewerId.HasValue)
            interviews = interviews.Where(i => i.InterviewerUserIds.Contains(query.InterviewerId.Value));
        if (query.ApplicationId.HasValue)
            interviews = interviews.Where(i => i.ApplicationId == query.ApplicationId.Value);

        var totalCount = await interviews.CountAsync(ct);

        // Materialise the page, then map in memory: the uuid[] interviewer list comes back with the
        // row, so there is no need to project a primitive collection into the DTO in SQL.
        var rows = await interviews
            .OrderBy(i => i.ScheduledAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Candidate names live in the Applications module; resolve them for the page in one cross-module
        // call (the interview row carries only the application id). Keeps the Interviews module unaware
        // of the Applications schema.
        var applicationIds = rows.Select(i => i.ApplicationId).Distinct().ToList();
        var candidateNames = await _applications.GetCandidateNamesByApplicationAsync(applicationIds, ct);

        var items = rows
            .Select(i => new InterviewListItemDto(
                i.Id, i.ApplicationId, candidateNames.GetValueOrDefault(i.ApplicationId, string.Empty),
                i.Type.ToString(), i.ScheduledAtUtc, i.DurationMinutes, i.Status.ToString(),
                i.InterviewerUserIds.ToList()))
            .ToList();

        return Result.Success(new PagedResult<InterviewListItemDto>(items, page, pageSize, totalCount));
    }
}

// ---- GetInterviewById (detail) ----
public sealed record GetInterviewByIdQuery(Guid Id) : IQuery<InterviewDetailDto>;

public sealed class GetInterviewByIdHandler : IQueryHandler<GetInterviewByIdQuery, InterviewDetailDto>
{
    private readonly IInterviewsDbContext _db;
    public GetInterviewByIdHandler(IInterviewsDbContext db) => _db = db;

    public async Task<Result<InterviewDetailDto>> Handle(GetInterviewByIdQuery query, CancellationToken ct)
    {
        var interview = await _db.Interviews.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == query.Id, ct);

        if (interview is null)
            return Result.Failure<InterviewDetailDto>(InterviewErrors.NotFound);

        return Result.Success(new InterviewDetailDto(
            interview.Id, interview.ApplicationId, interview.Type.ToString(), interview.ScheduledAtUtc,
            interview.DurationMinutes, interview.Status.ToString(),
            interview.Notes, interview.InterviewerUserIds.ToList(), interview.RoomToken));
    }
}
