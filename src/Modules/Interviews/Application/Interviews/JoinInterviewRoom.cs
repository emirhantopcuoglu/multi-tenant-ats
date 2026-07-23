using Ats.Modules.Interviews.Domain;
using Ats.Shared.Contracts.Applications;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Interviews.Application.Interviews;

// The four states a room can be in for a caller who is otherwise allowed to see it. The web client
// renders each one differently (countdown, join panel, "the interview has ended", ...). Unavailable
// covers every non-Scheduled status (cancelled/completed/no-show) with one label rather than
// exposing the interview's internal lifecycle to a room visitor.
public enum InterviewRoomState { TooEarly, Open, Ended, Unavailable }

public sealed record InterviewRoomDto(
    Guid InterviewId,
    string JobTitle,
    string Type,
    DateTime ScheduledAtUtc,
    int DurationMinutes,
    string State,
    DateTime OpensAtUtc);

// Exactly one of CandidateAccountId or (CompanyUserId, CompanyTenantId) is set — the controller
// decides which, from the token_type claim, before sending this. A caller with neither is not
// something the controller can produce ([Authorize] already requires a valid token of one kind).
public sealed record JoinInterviewRoomQuery(
    string RoomToken, Guid? CandidateAccountId, Guid? CompanyUserId, Guid? CompanyTenantId)
    : IQuery<InterviewRoomDto>;

public sealed class JoinInterviewRoomHandler : IQueryHandler<JoinInterviewRoomQuery, InterviewRoomDto>
{
    private readonly IInterviewsDbContext _db;
    private readonly IApplicationDirectory _applications;

    public JoinInterviewRoomHandler(IInterviewsDbContext db, IApplicationDirectory applications)
    {
        _db = db;
        _applications = applications;
    }

    public async Task<Result<InterviewRoomDto>> Handle(JoinInterviewRoomQuery query, CancellationToken ct)
    {
        // No ambient tenant to rely on: a candidate has none, and a company caller's tenant may not
        // even be the interview's tenant yet (that's exactly what we're about to check) — the token
        // in the URL is what identifies the row, the same reasoning as the unique index on it.
        var interview = await _db.Interviews
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.RoomToken == query.RoomToken, ct);

        if (interview is null)
            return Result.Failure<InterviewRoomDto>(InterviewErrors.NotFound);

        var application = await _applications.GetForSchedulingAsync(interview.TenantId, interview.ApplicationId, ct);
        if (application is null)
            return Result.Failure<InterviewRoomDto>(InterviewErrors.NotFound);

        if (!IsAuthorized(query, interview, application))
            // Wrong candidate, wrong company, or an uninvited interviewer: indistinguishable from a
            // bad token. Revealing which case applies would leak who the real participants are.
            return Result.Failure<InterviewRoomDto>(InterviewErrors.NotFound);

        return Result.Success(new InterviewRoomDto(
            interview.Id, application.JobTitle, interview.Type.ToString(), interview.ScheduledAtUtc,
            interview.DurationMinutes, ResolveState(interview, DateTime.UtcNow).ToString(),
            interview.RoomOpensAtUtc));
    }

    private static bool IsAuthorized(
        JoinInterviewRoomQuery query, Interview interview, ApplicationForScheduling application) =>
        query.CandidateAccountId is { } candidateAccountId
            ? application.CandidateAccountId == candidateAccountId
            : query.CompanyUserId is { } companyUserId
                && query.CompanyTenantId == interview.TenantId
                && interview.InterviewerUserIds.Contains(companyUserId);

    private static InterviewRoomState ResolveState(Interview interview, DateTime nowUtc) =>
        interview.Status != InterviewStatus.Scheduled ? InterviewRoomState.Unavailable
        : nowUtc < interview.RoomOpensAtUtc ? InterviewRoomState.TooEarly
        : nowUtc > interview.RoomClosesAtUtc ? InterviewRoomState.Ended
        : InterviewRoomState.Open;
}
