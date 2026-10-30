using Ats.Modules.Interviews.Application.Events;
using Ats.Modules.Interviews.Domain;
using Ats.Shared.Contracts.Applications;
using Ats.Shared.Kernel;
using FluentValidation;
using MediatR;

namespace Ats.Modules.Interviews.Application.Interviews;

public sealed record ScheduleInterviewCommand(
    Guid ApplicationId,
    InterviewType Type,
    DateTime ScheduledAtUtc,
    int DurationMinutes,
    IReadOnlyList<Guid> InterviewerUserIds,
    string? Notes) : ICommand<Guid>;

public sealed class ScheduleInterviewValidator : AbstractValidator<ScheduleInterviewCommand>
{
    public ScheduleInterviewValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.ScheduledAtUtc)
            .GreaterThanOrEqualTo(_ => DateTime.UtcNow.AddMinutes(Interview.MinimumLeadMinutes))
            .WithMessage(
                $"The interview must be scheduled at least {Interview.MinimumLeadMinutes} minutes ahead.");
        RuleFor(x => x.DurationMinutes)
            .Must(Interview.AllowedDurationMinutes.Contains)
            .WithMessage("Duration must be one of the allowed presets.");
        RuleFor(x => x.Notes).MaximumLength(5000);
        RuleFor(x => x.InterviewerUserIds)
            .NotEmpty().WithMessage("At least one interviewer is required.");
        RuleForEach(x => x.InterviewerUserIds).NotEmpty();
    }
}

public sealed class ScheduleInterviewHandler : ICommandHandler<ScheduleInterviewCommand, Guid>
{
    private readonly IInterviewsDbContext _db;
    private readonly IApplicationDirectory _applications;
    private readonly IPublisher _publisher;
    private readonly ICurrentTenant _currentTenant;

    public ScheduleInterviewHandler(
        IInterviewsDbContext db,
        IApplicationDirectory applications,
        IPublisher publisher,
        ICurrentTenant currentTenant)
    {
        _db = db;
        _applications = applications;
        _publisher = publisher;
        _currentTenant = currentTenant;
    }

    public async Task<Result<Guid>> Handle(ScheduleInterviewCommand command, CancellationToken ct)
    {
        // Application must exist in this tenant and still be active.
        var application = await _applications.GetForSchedulingAsync(command.ApplicationId, ct);
        if (application is null)
            return Result.Failure<Guid>(InterviewErrors.ApplicationNotFound);
        if (!application.IsActive)
            return Result.Failure<Guid>(InterviewErrors.ApplicationNotActive);

        // All of the candidate's applications here, so double-booking across jobs is caught.
        var candidateApplicationIds = await _applications.GetApplicationIdsForCandidateAsync(
            application.CandidateId, ct);

        var conflict = await InterviewConflictGuard.CheckAsync(
            _db, command.ScheduledAtUtc, command.DurationMinutes, command.InterviewerUserIds,
            candidateApplicationIds, excludeInterviewId: null, ct);
        if (conflict is not null)
            return Result.Failure<Guid>(conflict);

        Interview interview;
        try
        {
            // The validator already reports these as 400s; the entity guards them anyway.
            interview = Interview.Schedule(
                command.ApplicationId, command.Type, command.ScheduledAtUtc, command.DurationMinutes,
                command.InterviewerUserIds, DateTime.UtcNow, command.Notes);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<Guid>(InterviewErrors.InvalidOperation(ex.Message));
        }

        _db.Interviews.Add(interview);
        await _db.SaveChangesAsync(ct);

        // Published after the save: this module has no outbox, so the bridge sends straight to the broker
        // and logs a failure. No recruiter notes in the event.
        await _publisher.Publish(
            new InterviewScheduledEvent(
                interview.Id, application.Id, application.JobId, application.JobTitle,
                application.CandidateId, application.CandidateAccountId,
                application.CandidateEmail, application.CandidateFirstName,
                interview.Type, interview.ScheduledAtUtc, interview.DurationMinutes,
                interview.RoomToken, _currentTenant.TenantId ?? Guid.Empty),
            ct);

        return Result.Success(interview.Id);
    }
}
