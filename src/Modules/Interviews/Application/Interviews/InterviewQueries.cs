using Ats.Modules.Interviews.Domain;
using Ats.Shared.Contracts.Applications;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Interviews.Application.Interviews;

// IsAwaitingOutcome travels with the row rather than being recomputed on the client: the rule is
// domain logic, and a browser deriving it from its own clock is how the list and the detail screen
// end up disagreeing about the same interview.
public sealed record InterviewListItemDto(
    Guid Id, Guid ApplicationId, string CandidateName, string Type, DateTime ScheduledAtUtc,
    int DurationMinutes, string Status, IReadOnlyList<Guid> InterviewerUserIds,
    bool IsAwaitingOutcome);

// The detail DTO additionally carries what the caller is allowed to *do*. The buttons are rendered
// straight from these flags instead of from a client-side reading of the status and the clock —
// the drift between those two is the bug this slice exists to fix.
public sealed record InterviewDetailDto(
    Guid Id, Guid ApplicationId, string Type, DateTime ScheduledAtUtc, int DurationMinutes,
    string Status, string? Notes, IReadOnlyList<Guid> InterviewerUserIds, string? RoomToken,
    bool IsAwaitingOutcome, bool CanReschedule, bool CanCancel, bool CanComplete,
    bool CanMarkNoShow, bool CanReassignInterviewers, bool CanReceiveFeedback,
    // The outcome details, each null unless the interview reached that state. CancellationNote is
    // included because this DTO serves the company side only — the candidate's view of an interview
    // is built by InterviewDirectory, which has no field for it.
    string? CancellationReason, string? CancellationNote, string? NoShowParty);

// A list bucket, not a stored status. Upcoming and AwaitingOutcome are both slices of
// Status.Scheduled cut by the clock — precisely the distinction the status column cannot express on
// its own, and the reason "show me what still needs a decision" was unanswerable before.
public enum InterviewListFilter { Upcoming, AwaitingOutcome, Completed, Cancelled, NoShow }

// ---- ListInterviews (filtered by date range / interviewer / bucket, paginated) ----
public sealed record ListInterviewsQuery(
    DateTime? FromDate = null, DateTime? ToDate = null, Guid? InterviewerId = null,
    Guid? ApplicationId = null, InterviewListFilter? Filter = null, int Page = 1, int PageSize = 20)
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
        var nowUtc = DateTime.UtcNow;

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

        // The end-time arithmetic is inlined rather than reusing Interview.EndsAtUtc: that property is
        // a C# getter EF cannot translate, so it would silently drag the whole table into memory.
        // Npgsql turns AddMinutes over a column into a native interval addition.
        interviews = query.Filter switch
        {
            InterviewListFilter.Upcoming => interviews.Where(
                i => i.Status == InterviewStatus.Scheduled
                  && i.ScheduledAtUtc.AddMinutes(i.DurationMinutes) > nowUtc),
            InterviewListFilter.AwaitingOutcome => interviews.Where(
                i => i.Status == InterviewStatus.Scheduled
                  && i.ScheduledAtUtc.AddMinutes(i.DurationMinutes) <= nowUtc),
            InterviewListFilter.Completed => interviews.Where(i => i.Status == InterviewStatus.Completed),
            InterviewListFilter.Cancelled => interviews.Where(i => i.Status == InterviewStatus.Cancelled),
            InterviewListFilter.NoShow => interviews.Where(i => i.Status == InterviewStatus.NoShow),
            _ => interviews,
        };

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
                i.InterviewerUserIds.ToList(), i.IsAwaitingOutcome(nowUtc)))
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

        var nowUtc = DateTime.UtcNow;

        return Result.Success(new InterviewDetailDto(
            interview.Id, interview.ApplicationId, interview.Type.ToString(), interview.ScheduledAtUtc,
            interview.DurationMinutes, interview.Status.ToString(),
            interview.Notes, interview.InterviewerUserIds.ToList(), interview.RoomToken,
            interview.IsAwaitingOutcome(nowUtc),
            interview.CanReschedule(nowUtc), interview.CanCancel(nowUtc),
            interview.CanComplete(nowUtc), interview.CanMarkNoShow(nowUtc),
            interview.CanReassignInterviewers(nowUtc), interview.CanReceiveFeedback(nowUtc),
            interview.CancellationReason?.ToString(), interview.CancellationNote,
            interview.NoShowParty?.ToString()));
    }
}
