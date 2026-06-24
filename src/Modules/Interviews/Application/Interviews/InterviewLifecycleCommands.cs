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
    private const int MaxDurationMinutes = 8 * 60;

    public RescheduleInterviewValidator()
    {
        RuleFor(x => x.InterviewId).NotEmpty();
        RuleFor(x => x.ScheduledAtUtc)
            .GreaterThan(_ => DateTime.UtcNow)
            .WithMessage("The interview must be scheduled in the future.");
        RuleFor(x => x.DurationMinutes).InclusiveBetween(1, MaxDurationMinutes);
    }
}

public sealed class RescheduleInterviewHandler : ICommandHandler<RescheduleInterviewCommand, bool>
{
    private readonly IInterviewsDbContext _db;
    public RescheduleInterviewHandler(IInterviewsDbContext db) => _db = db;

    public async Task<Result<bool>> Handle(RescheduleInterviewCommand command, CancellationToken ct)
    {
        var interview = await _db.Interviews.FirstOrDefaultAsync(i => i.Id == command.InterviewId, ct);
        if (interview is null)
            return Result.Failure<bool>(InterviewErrors.NotFound);

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
