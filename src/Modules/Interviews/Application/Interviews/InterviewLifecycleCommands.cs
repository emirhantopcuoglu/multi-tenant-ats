using Ats.Modules.Interviews.Application.Events;
using Ats.Modules.Interviews.Domain;
using Ats.Shared.Contracts.Applications;
using Ats.Shared.Kernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Interviews.Application.Interviews;

// The interview lifecycle transitions, all sharing the same shape: load the interview, ask the entity
// to change state (it guards the invariant that a terminal interview can't transition), then save. No
// integration events yet — emailing/reminders on cancel land in a later slice.

// ---- Reschedule ----
public sealed record RescheduleInterviewCommand(
    Guid InterviewId, DateTime ScheduledAtUtc, int DurationMinutes) : ICommand<bool>;

public sealed class RescheduleInterviewValidator : AbstractValidator<RescheduleInterviewCommand>
{
    public RescheduleInterviewValidator()
    {
        RuleFor(x => x.InterviewId).NotEmpty();
        RuleFor(x => x.ScheduledAtUtc)
            .GreaterThanOrEqualTo(_ => DateTime.UtcNow.AddMinutes(Interview.MinimumLeadMinutes))
            .WithMessage(
                $"The interview must be scheduled at least {Interview.MinimumLeadMinutes} minutes ahead.");
        RuleFor(x => x.DurationMinutes)
            .Must(Interview.AllowedDurationMinutes.Contains)
            .WithMessage("Duration must be one of the allowed presets.");
    }
}

public sealed class RescheduleInterviewHandler : ICommandHandler<RescheduleInterviewCommand, bool>
{
    private readonly IInterviewsDbContext _db;
    private readonly IApplicationDirectory _applications;
    private readonly IPublisher _publisher;
    private readonly ICurrentTenant _currentTenant;

    public RescheduleInterviewHandler(
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

    public async Task<Result<bool>> Handle(RescheduleInterviewCommand command, CancellationToken ct)
    {
        var interview = await _db.Interviews.FirstOrDefaultAsync(i => i.Id == command.InterviewId, ct);
        if (interview is null)
            return Result.Failure<bool>(InterviewErrors.NotFound);

        // Resolve the candidate behind the interview so the overlap check catches the candidate being
        // double-booked too, not only an interviewer. The application always exists in this tenant
        // (the interview was scheduled against it); an empty set just skips the candidate check.
        var application = await _applications.GetForSchedulingAsync(interview.ApplicationId, ct);
        IReadOnlyList<Guid> candidateApplicationIds = application is null
            ? []
            : await _applications.GetApplicationIdsForCandidateAsync(application.CandidateId, ct);

        // The new time must not collide with the interviewers' or the candidate's other interviews;
        // this interview itself is excluded so it never conflicts with its own old slot.
        var conflict = await InterviewConflictGuard.CheckAsync(
            _db, command.ScheduledAtUtc, command.DurationMinutes, interview.InterviewerUserIds,
            candidateApplicationIds, excludeInterviewId: interview.Id, ct);
        if (conflict is not null)
            return Result.Failure<bool>(conflict);

        // Captured before the move: the candidate is holding this time, so the notification has to
        // be able to say what it changed from, not only what it changed to.
        var previousScheduledAtUtc = interview.ScheduledAtUtc;

        try
        {
            interview.Reschedule(command.ScheduledAtUtc, command.DurationMinutes, DateTime.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<bool>(InterviewErrors.TransitionNotAllowed(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<bool>(InterviewErrors.InvalidOperation(ex.Message));
        }

        await _db.SaveChangesAsync(ct);

        // Published after the commit, like ScheduleInterview and for the same reason: this module
        // has no transactional outbox, and announcing a move that then failed to save would be a
        // lie. A missing application (deleted between the read above and here) only costs the
        // notification — the reschedule itself already stands.
        if (application is not null)
        {
            await _publisher.Publish(
                new InterviewRescheduledEvent(
                    interview.Id, application.Id, application.JobId, application.JobTitle,
                    application.CandidateId, application.CandidateAccountId,
                    application.CandidateEmail, application.CandidateFirstName,
                    interview.Type, previousScheduledAtUtc, interview.ScheduledAtUtc,
                    interview.DurationMinutes, interview.RoomToken,
                    _currentTenant.TenantId ?? Guid.Empty),
                ct);
        }

        return Result.Success(true);
    }
}

// Shared by the two no-argument transitions (complete/no-show), which differ only by the domain
// method they call. Reschedule and cancel are kept separate: both take parameters and both raise a
// candidate-facing event, so there is nothing left for them to share here.
internal static class InterviewTransition
{
    // The clock is read once here and handed to the transition, so a single request can never see two
    // different "now"s — the guard and any timestamp it writes agree by construction.
    public static async Task<Result<bool>> ApplyAsync(
        IInterviewsDbContext db, Guid interviewId,
        Action<Domain.Interview, DateTime> transition, CancellationToken ct)
    {
        var interview = await db.Interviews.FirstOrDefaultAsync(i => i.Id == interviewId, ct);
        if (interview is null)
            return Result.Failure<bool>(InterviewErrors.NotFound);

        try
        {
            transition(interview, DateTime.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<bool>(InterviewErrors.TransitionNotAllowed(ex.Message));
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

// ---- Cancel ----
// Unlike complete/no-show this one does not use InterviewTransition: cancelling is the only
// transition the candidate must hear about, so the handler also resolves their contact details and
// raises a domain event.
public sealed record CancelInterviewCommand(
    Guid InterviewId, InterviewCancellationReason Reason, string? Note) : ICommand<bool>;

public sealed class CancelInterviewValidator : AbstractValidator<CancelInterviewCommand>
{
    private const int MaxNoteLength = 500;

    public CancelInterviewValidator()
    {
        RuleFor(x => x.InterviewId).NotEmpty();
        RuleFor(x => x.Reason).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(MaxNoteLength);
    }
}

public sealed class CancelInterviewHandler : ICommandHandler<CancelInterviewCommand, bool>
{
    private readonly IInterviewsDbContext _db;
    private readonly IApplicationDirectory _applications;
    private readonly IPublisher _publisher;
    private readonly ICurrentTenant _currentTenant;

    public CancelInterviewHandler(
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

    public async Task<Result<bool>> Handle(CancelInterviewCommand command, CancellationToken ct)
    {
        var interview = await _db.Interviews.FirstOrDefaultAsync(i => i.Id == command.InterviewId, ct);
        if (interview is null)
            return Result.Failure<bool>(InterviewErrors.NotFound);

        try
        {
            interview.Cancel(command.Reason, command.Note, DateTime.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<bool>(InterviewErrors.TransitionNotAllowed(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<bool>(InterviewErrors.InvalidOperation(ex.Message));
        }

        await _db.SaveChangesAsync(ct);

        // Read after the commit, not before: nothing above needs the application, and a lookup that
        // only feeds a best-effort notification must not sit between the guard and the save.
        var application = await _applications.GetForSchedulingAsync(interview.ApplicationId, ct);
        if (application is not null)
        {
            await _publisher.Publish(
                new InterviewCancelledEvent(
                    interview.Id, application.Id, application.JobId, application.JobTitle,
                    application.CandidateId, application.CandidateAccountId,
                    application.CandidateEmail, application.CandidateFirstName,
                    interview.Type, interview.ScheduledAtUtc, command.Reason,
                    _currentTenant.TenantId ?? Guid.Empty),
                ct);
        }

        return Result.Success(true);
    }
}

// ---- Complete ----
public sealed record CompleteInterviewCommand(Guid InterviewId) : ICommand<bool>;

public sealed class CompleteInterviewValidator : AbstractValidator<CompleteInterviewCommand>
{
    public CompleteInterviewValidator() => RuleFor(x => x.InterviewId).NotEmpty();
}

public sealed class CompleteInterviewHandler : ICommandHandler<CompleteInterviewCommand, bool>
{
    private readonly IInterviewsDbContext _db;
    public CompleteInterviewHandler(IInterviewsDbContext db) => _db = db;

    public Task<Result<bool>> Handle(CompleteInterviewCommand command, CancellationToken ct) =>
        InterviewTransition.ApplyAsync(_db, command.InterviewId, (i, now) => i.Complete(now), ct);
}

// ---- Mark no-show ----
public sealed record MarkInterviewNoShowCommand(Guid InterviewId) : ICommand<bool>;

public sealed class MarkInterviewNoShowValidator : AbstractValidator<MarkInterviewNoShowCommand>
{
    public MarkInterviewNoShowValidator() => RuleFor(x => x.InterviewId).NotEmpty();
}

public sealed class MarkInterviewNoShowHandler : ICommandHandler<MarkInterviewNoShowCommand, bool>
{
    private readonly IInterviewsDbContext _db;
    public MarkInterviewNoShowHandler(IInterviewsDbContext db) => _db = db;

    public Task<Result<bool>> Handle(MarkInterviewNoShowCommand command, CancellationToken ct) =>
        InterviewTransition.ApplyAsync(_db, command.InterviewId, (i, now) => i.MarkNoShow(now), ct);
}
