using Ats.Modules.Interviews.Domain;
using Ats.Shared.Contracts.Applications;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Interviews.Application.Interviews;

// Unavailable covers every non-Scheduled status, so visitors don't see the internal lifecycle.
public enum InterviewRoomState { TooEarly, Open, Ended, Unavailable }

public sealed record InterviewRoomDto(
    Guid InterviewId,
    string JobTitle,
    string Type,
    DateTime ScheduledAtUtc,
    int DurationMinutes,
    string State,
    DateTime OpensAtUtc);

// Exactly one of CandidateAccountId or (CompanyUserId, CompanyTenantId) is set, by the controller.
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
        // No ambient tenant: the token identifies the row, and the participant check comes next.
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
            // Same answer as a bad token, so it doesn't reveal who the participants are.
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
