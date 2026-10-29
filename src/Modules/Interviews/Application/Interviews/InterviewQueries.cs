using Ats.Modules.Interviews.Domain;
using Ats.Shared.Contracts.Applications;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Interviews.Application.Interviews;

// Computed on the server so the list and the detail page always agree.
public sealed record InterviewListItemDto(
    Guid Id, Guid ApplicationId, string CandidateName, string Type, DateTime ScheduledAtUtc,
    int DurationMinutes, string Status, IReadOnlyList<Guid> InterviewerUserIds,
    bool IsAwaitingOutcome);

// The Can* flags drive the UI buttons, so the client doesn't re-derive them from the clock.
public sealed record InterviewDetailDto(
    Guid Id, Guid ApplicationId, string Type, DateTime ScheduledAtUtc, int DurationMinutes,
    string Status, string? Notes, IReadOnlyList<Guid> InterviewerUserIds, string? RoomToken,
    bool IsAwaitingOutcome, bool CanReschedule, bool CanCancel, bool CanComplete,
    bool CanMarkNoShow, bool CanReassignInterviewers, bool CanReceiveFeedback,
    // CancellationNote is fine here: this DTO is company-side only.
    string? CancellationReason, string? CancellationNote, string? NoShowParty);

// Upcoming and AwaitingOutcome are both Scheduled, split by the clock.
public enum InterviewListFilter { Upcoming, AwaitingOutcome, Completed, Cancelled, NoShow }

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
        // Translates to "@interviewerId = ANY(InterviewerUserIds)".
        if (query.InterviewerId.HasValue)
            interviews = interviews.Where(i => i.InterviewerUserIds.Contains(query.InterviewerId.Value));
        if (query.ApplicationId.HasValue)
            interviews = interviews.Where(i => i.ApplicationId == query.ApplicationId.Value);

        // Inlined because EF can't translate Interview.EndsAtUtc; Npgsql handles AddMinutes on a column.
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

        var rows = await interviews
            .OrderBy(i => i.ScheduledAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Candidate names come from the Applications module in one batched call.
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
