using Ats.Modules.Interviews.Application.Events;
using Ats.Modules.Interviews.Domain;
using Ats.Shared.Contracts.Applications;
using Ats.Shared.Kernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Ats.Modules.Interviews.Application.Interviews;


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

        // So the overlap check also covers the candidate. An empty set skips that part.
        var application = await _applications.GetForSchedulingAsync(interview.ApplicationId, ct);
        IReadOnlyList<Guid> candidateApplicationIds = application is null
            ? []
            : await _applications.GetApplicationIdsForCandidateAsync(application.CandidateId, ct);

        // This interview is excluded so it doesn't conflict with its own old slot.
        var conflict = await InterviewConflictGuard.CheckAsync(
            _db, command.ScheduledAtUtc, command.DurationMinutes, interview.InterviewerUserIds,
            candidateApplicationIds, excludeInterviewId: interview.Id, ct);
        if (conflict is not null)
            return Result.Failure<bool>(conflict);

        // Kept so the notification can show the old time.
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

        // Published after the save (no outbox in this module). A missing application only skips the
        // notification.
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

// Shared by complete and no-show. Reschedule and cancel take parameters and raise events.
internal static class InterviewTransition
{
    // One clock read per request, so the guard and the timestamps agree.
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

// Cancelling notifies the candidate, so it resolves their details and raises an event.
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

        // Looked up after the save; it only feeds the notification.
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

public sealed record MarkInterviewNoShowCommand(Guid InterviewId, NoShowParty Party) : ICommand<bool>;

public sealed class MarkInterviewNoShowValidator : AbstractValidator<MarkInterviewNoShowCommand>
{
    public MarkInterviewNoShowValidator()
    {
        RuleFor(x => x.InterviewId).NotEmpty();
        RuleFor(x => x.Party).IsInEnum();
    }
}

public sealed class MarkInterviewNoShowHandler : ICommandHandler<MarkInterviewNoShowCommand, bool>
{
    private readonly IInterviewsDbContext _db;
    public MarkInterviewNoShowHandler(IInterviewsDbContext db) => _db = db;

    public Task<Result<bool>> Handle(MarkInterviewNoShowCommand command, CancellationToken ct) =>
        InterviewTransition.ApplyAsync(
            _db, command.InterviewId, (i, now) => i.MarkNoShow(command.Party, now), ct);
}

// Separate from reschedule: changing who and changing when are different operations.
public sealed record ReassignInterviewersCommand(
    Guid InterviewId, IReadOnlyList<Guid> InterviewerUserIds) : ICommand<bool>;

public sealed class ReassignInterviewersValidator : AbstractValidator<ReassignInterviewersCommand>
{
    public ReassignInterviewersValidator()
    {
        RuleFor(x => x.InterviewId).NotEmpty();
        RuleFor(x => x.InterviewerUserIds)
            .NotEmpty().WithMessage("At least one interviewer is required.");
        RuleForEach(x => x.InterviewerUserIds).NotEmpty();
    }
}

public sealed class ReassignInterviewersHandler : ICommandHandler<ReassignInterviewersCommand, bool>
{
    private readonly IInterviewsDbContext _db;

    public ReassignInterviewersHandler(IInterviewsDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(ReassignInterviewersCommand command, CancellationToken ct)
    {
        var interview = await _db.Interviews.FirstOrDefaultAsync(i => i.Id == command.InterviewId, ct);
        if (interview is null)
            return Result.Failure<bool>(InterviewErrors.NotFound);

        // The slot doesn't move, so only new interviewers can conflict; the candidate isn't re-checked.
        var conflict = await InterviewConflictGuard.CheckAsync(
            _db, interview.ScheduledAtUtc, interview.DurationMinutes, command.InterviewerUserIds,
            candidateApplicationIds: [], excludeInterviewId: interview.Id, ct);
        if (conflict is not null)
            return Result.Failure<bool>(conflict);

        try
        {
            interview.ReassignInterviewers(command.InterviewerUserIds, DateTime.UtcNow);
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

        // No event: no contract carries interviewer names, and interviewers get no notifications.
        return Result.Success(true);
    }
}
