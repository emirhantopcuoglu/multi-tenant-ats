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
    private readonly ICurrentUser _currentUser;
    private readonly IActivityLogRepository _activityLog;
    private readonly ILogger<MoveApplicationStageHandler> _logger;

    public MoveApplicationStageHandler(
        IApplicationsDbContext db,
        IPublisher publisher,
        ICurrentUser currentUser,
        IActivityLogRepository activityLog,
        ILogger<MoveApplicationStageHandler> logger)
    {
        _db = db;
        _publisher = publisher;
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

        await _db.SaveChangesAsync(ct);

        // Log the move after the state change is committed — the activity log is in MongoDB now,
        // outside this transaction. A failed log write is warned and swallowed, not propagated.
        await _activityLog.TryAddAsync(
            ApplicationActivity.StageChanged(
                application.Id, _currentUser.UserId, fromStageId, command.TargetStageId),
            _logger, ct);

        await _publisher.Publish(
            new ApplicationStageChangedEvent(
                application.Id, fromStageId, command.TargetStageId, application.TenantId),
            ct);

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

        await _db.SaveChangesAsync(ct);

        // Logged best-effort after commit; see MoveApplicationStageHandler for the rationale.
        await _activityLog.TryAddAsync(
            ApplicationActivity.Rejected(application.Id, _currentUser.UserId, command.Reason),
            _logger, ct);

        // Notify the candidate out-of-process. Gather the data the email needs — the candidate's
        // name/email (this module) and the job title (the Jobs module, via the read port) — and
        // raise an in-process event; a bridge handler forwards it to the bus. The internal reason
        // is never passed on. The candidate is loaded fresh rather than carried on the entity
        // because Application references it by id only.
        var candidate = await _db.Candidates.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == application.CandidateId, ct);
        if (candidate is not null)
        {
            var jobTitle = await _jobs.GetJobTitleByIdAsync(application.JobId, ct);
            await _publisher.Publish(
                new ApplicationRejectedEvent(
                    application.Id, application.JobId, jobTitle ?? string.Empty,
                    candidate.Id, candidate.Email, candidate.FirstName, application.TenantId),
                ct);
        }

        return Result.Success(true);
    }
}
