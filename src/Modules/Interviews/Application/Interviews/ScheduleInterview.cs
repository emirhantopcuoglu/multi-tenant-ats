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
            .GreaterThan(_ => DateTime.UtcNow)
            .WithMessage("The interview must be scheduled in the future.");
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
        // Cross-module read: confirm the application exists in this tenant and is still open. The
        // Interviews module never references Applications directly — it asks through the contract,
        // and the global query filter on the other side keeps the lookup tenant-scoped.
        var application = await _applications.GetForSchedulingAsync(command.ApplicationId, ct);
        if (application is null)
            return Result.Failure<Guid>(InterviewErrors.ApplicationNotFound);
        if (!application.IsActive)
            return Result.Failure<Guid>(InterviewErrors.ApplicationNotActive);

        // Every application the same candidate holds in this tenant, so the overlap check below can
        // catch a candidate double-booked across two different job applications, not just this one.
        var candidateApplicationIds = await _applications.GetApplicationIdsForCandidateAsync(
            application.CandidateId, ct);

        // Reject a schedule that would put an interviewer (or the candidate) in two places at once.
        var conflict = await InterviewConflictGuard.CheckAsync(
            _db, command.ScheduledAtUtc, command.DurationMinutes, command.InterviewerUserIds,
            candidateApplicationIds, excludeInterviewId: null, ct);
        if (conflict is not null)
            return Result.Failure<Guid>(conflict);

        Interview interview;
        try
        {
            // The entity owns the scheduling invariants (future time, preset duration, at least one
            // interviewer). The validator already reported these as 400s; this guards them regardless.
            interview = Interview.Schedule(
                command.ApplicationId, command.Type, command.ScheduledAtUtc, command.DurationMinutes,
                command.InterviewerUserIds, command.Notes);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<Guid>(InterviewErrors.InvalidOperation(ex.Message));
        }

        _db.Interviews.Add(interview);
        await _db.SaveChangesAsync(ct);

        // Published AFTER SaveChanges: this module has no transactional outbox (MassTransit 8.x
        // allows only one per container and Applications holds it — see Program.cs), so the bridge
        // sends directly to the broker. Announcing before the commit could notify the candidate of
        // an interview that then fails to save; announcing after means a broker outage can lose the
        // notification, which the bridge logs — the lesser failure. The tenant id comes from the
        // ambient tenant because the save-changes interceptor stamps interview.TenantId from that
        // same source. The application read model supplies the candidate contact and job title;
        // the recruiter's notes stay out of the event on purpose.
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
