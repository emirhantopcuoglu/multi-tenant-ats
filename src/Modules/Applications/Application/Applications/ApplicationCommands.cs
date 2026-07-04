using Ats.Modules.Applications.Application.Events;
using Ats.Modules.Applications.Domain;
using Ats.Shared.Contracts.Jobs;
using Ats.Shared.Kernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Applications.Application.Applications;

// ---- MoveStage ----
public sealed record MoveApplicationStageCommand(Guid ApplicationId, Guid TargetStageId)
    : ICommand<bool>;

public sealed class MoveApplicationStageValidator : AbstractValidator<MoveApplicationStageCommand>
{
    public MoveApplicationStageValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
        RuleFor(x => x.TargetStageId).NotEmpty();
    }
}

public sealed class MoveApplicationStageHandler : ICommandHandler<MoveApplicationStageCommand, bool>
{
    private readonly IApplicationsDbContext _db;
    private readonly IPublisher _publisher;
    private readonly IJobDirectory _jobs;
    private readonly ICurrentUser _currentUser;
    private readonly IActivityLogRepository _activityLog;
    private readonly ILogger<MoveApplicationStageHandler> _logger;

    public MoveApplicationStageHandler(
        IApplicationsDbContext db,
        IPublisher publisher,
        IJobDirectory jobs,
        ICurrentUser currentUser,
        IActivityLogRepository activityLog,
        ILogger<MoveApplicationStageHandler> logger)
    {
        _db = db;
        _publisher = publisher;
        _jobs = jobs;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(MoveApplicationStageCommand command, CancellationToken ct)
    {
        var application = await _db.Applications
            .FirstOrDefaultAsync(a => a.Id == command.ApplicationId, ct);
        if (application is null)
            return Result.Failure<bool>(ApplicationErrors.NotFound);

        // Cross-aggregate rule: the target stage must belong to this job's pipeline. The entity
        // can't check this (it doesn't hold the pipeline), so the handler does it here.
        var pipeline = await _db.Pipelines
            .Include(p => p.Stages)
            .FirstOrDefaultAsync(p => p.JobId == application.JobId, ct);
        if (pipeline is null || pipeline.Stages.All(s => s.Id != command.TargetStageId))
            return Result.Failure<bool>(ApplicationErrors.StageNotInPipeline);

        var fromStageId = application.CurrentStageId;

        try
        {
            // The entity guards the invariant that a terminal application can't be moved.
            application.MoveToStage(command.TargetStageId);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<bool>(ApplicationErrors.InvalidOperation(ex.Message));
        }

        // Gather what the stage-changed integration event needs: the stage names come free from the
        // pipeline already loaded above, the candidate's contact from this module, the job title
        // from the Jobs read port. Mirrors RejectApplicationHandler — and like there, publish goes
        // BEFORE SaveChanges so the transactional outbox writes the message in the same transaction
        // as the stage move (atomic: both commit or neither).
        var candidate = await _db.Candidates.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == application.CandidateId, ct);
        if (candidate is not null)
        {
            var jobTitle = await _jobs.GetJobTitleByIdAsync(application.JobId, ct);
            var fromStageName = pipeline.Stages.FirstOrDefault(s => s.Id == fromStageId)?.Name;
            var toStageName = pipeline.Stages.First(s => s.Id == command.TargetStageId).Name;

            await _publisher.Publish(
                new ApplicationStageChangedEvent(
                    application.Id, application.JobId, jobTitle ?? string.Empty,
                    candidate.Id, candidate.Email, candidate.FirstName,
                    fromStageId, fromStageName ?? string.Empty,
                    command.TargetStageId, toStageName,
                    application.TenantId),
                ct);
        }

        await _db.SaveChangesAsync(ct);

        // Log the move after the state change is committed — the activity log is in MongoDB now,
        // outside this transaction. A failed log write is warned and swallowed, not propagated.
        await _activityLog.TryAddAsync(
            ApplicationActivity.StageChanged(
                application.Id, _currentUser.UserId, fromStageId, command.TargetStageId),
            _logger, ct);

        return Result.Success(true);
    }
}

// ---- MarkViewed (read receipt) ----
// Fired by the API layer when a company user opens the application detail. Deliberately a
// server-side effect of the trusted read path — there is no client-callable "mark viewed"
// endpoint, so a forged receipt cannot be produced. Idempotent: only the first view is
// recorded. Two simultaneous first views can race and both log an activity; the candidate
// projection reads only the earliest, so the race is harmless and left unguarded.
public sealed record MarkApplicationViewedCommand(Guid ApplicationId) : ICommand<bool>;

public sealed class MarkApplicationViewedHandler : ICommandHandler<MarkApplicationViewedCommand, bool>
{
    private readonly IApplicationsDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IActivityLogRepository _activityLog;
    private readonly ILogger<MarkApplicationViewedHandler> _logger;

    public MarkApplicationViewedHandler(
        IApplicationsDbContext db,
        ICurrentUser currentUser,
        IActivityLogRepository activityLog,
        ILogger<MarkApplicationViewedHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(MarkApplicationViewedCommand command, CancellationToken ct)
    {
        var application = await _db.Applications
            .FirstOrDefaultAsync(a => a.Id == command.ApplicationId, ct);
        if (application is null)
            return Result.Failure<bool>(ApplicationErrors.NotFound);

        if (!application.MarkViewed())
            return Result.Success(false);

        await _db.SaveChangesAsync(ct);

        // Logged best-effort after commit; see MoveApplicationStageHandler for the rationale.
        await _activityLog.TryAddAsync(
            ApplicationActivity.Viewed(application.Id, _currentUser.UserId), _logger, ct);

        return Result.Success(true);
    }
}

// ---- Reject ----
public sealed record RejectApplicationCommand(Guid ApplicationId, string Reason) : ICommand<bool>;

public sealed class RejectApplicationValidator : AbstractValidator<RejectApplicationCommand>
{
    public RejectApplicationValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed class RejectApplicationHandler : ICommandHandler<RejectApplicationCommand, bool>
{
    private readonly IApplicationsDbContext _db;
    private readonly IPublisher _publisher;
    private readonly IJobDirectory _jobs;
    private readonly ICurrentUser _currentUser;
    private readonly IActivityLogRepository _activityLog;
    private readonly ILogger<RejectApplicationHandler> _logger;

    public RejectApplicationHandler(
        IApplicationsDbContext db,
        IPublisher publisher,
        IJobDirectory jobs,
        ICurrentUser currentUser,
        IActivityLogRepository activityLog,
        ILogger<RejectApplicationHandler> logger)
    {
        _db = db;
        _publisher = publisher;
        _jobs = jobs;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(RejectApplicationCommand command, CancellationToken ct)
    {
        var application = await _db.Applications
            .FirstOrDefaultAsync(a => a.Id == command.ApplicationId, ct);
        if (application is null)
            return Result.Failure<bool>(ApplicationErrors.NotFound);

        try
        {
            application.Reject(command.Reason);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<bool>(ApplicationErrors.InvalidOperation(ex.Message));
        }

        // Gather the data the candidate's rejection email needs before saving — the candidate's
        // name/email (this module) and the job title (the Jobs module, via the read port). The
        // internal reason is never passed on. The candidate is loaded fresh rather than carried on
        // the entity because Application references it by id only.
        var candidate = await _db.Candidates.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == application.CandidateId, ct);
        if (candidate is not null)
        {
            var jobTitle = await _jobs.GetJobTitleByIdAsync(application.JobId, ct);
            // Publish before saving so the transactional outbox writes the integration event in the
            // same transaction as the rejected status — atomic, and never lost to a broker outage.
            await _publisher.Publish(
                new ApplicationRejectedEvent(
                    application.Id, application.JobId, jobTitle ?? string.Empty,
                    candidate.Id, candidate.Email, candidate.FirstName, application.TenantId),
                ct);
        }

        await _db.SaveChangesAsync(ct);

        // Logged best-effort after commit; see MoveApplicationStageHandler for the rationale.
        await _activityLog.TryAddAsync(
            ApplicationActivity.Rejected(application.Id, _currentUser.UserId, command.Reason),
            _logger, ct);

        return Result.Success(true);
    }
}
