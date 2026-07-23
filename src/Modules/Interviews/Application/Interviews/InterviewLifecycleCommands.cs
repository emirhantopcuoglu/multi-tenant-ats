using Ats.Modules.Interviews.Domain;
using Ats.Shared.Contracts.Applications;
using Ats.Shared.Kernel;
using FluentValidation;
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
            .GreaterThan(_ => DateTime.UtcNow)
            .WithMessage("The interview must be scheduled in the future.");
        RuleFor(x => x.DurationMinutes)
            .Must(Interview.AllowedDurationMinutes.Contains)
            .WithMessage("Duration must be one of the allowed presets.");
    }
}

public sealed class RescheduleInterviewHandler : ICommandHandler<RescheduleInterviewCommand, bool>
{
    private readonly IInterviewsDbContext _db;
    private readonly IApplicationDirectory _applications;

    public RescheduleInterviewHandler(IInterviewsDbContext db, IApplicationDirectory applications)
    {
        _db = db;
        _applications = applications;
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

        try
        {
            interview.Reschedule(command.ScheduledAtUtc, command.DurationMinutes);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Result.Failure<bool>(InterviewErrors.InvalidOperation(ex.Message));
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

// Shared by the three no-argument transitions (cancel/complete/no-show), which differ only by the
// domain method they call. Reschedule is kept separate: it takes parameters and its entity method can
// also throw ArgumentException for an invalid new time/duration.
internal static class InterviewTransition
{
    public static async Task<Result<bool>> ApplyAsync(
        IInterviewsDbContext db, Guid interviewId, Action<Domain.Interview> transition, CancellationToken ct)
    {
        var interview = await db.Interviews.FirstOrDefaultAsync(i => i.Id == interviewId, ct);
        if (interview is null)
            return Result.Failure<bool>(InterviewErrors.NotFound);

        try
        {
            transition(interview);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<bool>(InterviewErrors.InvalidOperation(ex.Message));
        }

        await db.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

// ---- Cancel ----
public sealed record CancelInterviewCommand(Guid InterviewId) : ICommand<bool>;

public sealed class CancelInterviewValidator : AbstractValidator<CancelInterviewCommand>
{
    public CancelInterviewValidator() => RuleFor(x => x.InterviewId).NotEmpty();
}

public sealed class CancelInterviewHandler : ICommandHandler<CancelInterviewCommand, bool>
{
    private readonly IInterviewsDbContext _db;
    public CancelInterviewHandler(IInterviewsDbContext db) => _db = db;

    public Task<Result<bool>> Handle(CancelInterviewCommand command, CancellationToken ct) =>
        InterviewTransition.ApplyAsync(_db, command.InterviewId, i => i.Cancel(), ct);
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
        InterviewTransition.ApplyAsync(_db, command.InterviewId, i => i.Complete(), ct);
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
        InterviewTransition.ApplyAsync(_db, command.InterviewId, i => i.MarkNoShow(), ct);
}
