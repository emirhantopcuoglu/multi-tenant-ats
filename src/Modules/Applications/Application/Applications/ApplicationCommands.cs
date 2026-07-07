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
        if (pipeline is null)
            return Result.Failure<bool>(ApplicationErrors.StageNotInPipeline);

        var targetStage = pipeline.Stages.FirstOrDefault(s => s.Id == command.TargetStageId);
        if (targetStage is null)
            return Result.Failure<bool>(ApplicationErrors.StageNotInPipeline);

        // Terminal stages are outcomes, not positions: reaching them must also flip the
        // application's status, which only the hire/reject commands do. Allowing them here let a
        // recruiter show a candidate "hired" (or park them in Rejected) while the application
        // stayed Active and fully movable.
        if (targetStage.Type is PipelineStageType.FinalHired or PipelineStageType.FinalRejected)
            return Result.Failure<bool>(ApplicationErrors.TerminalStageRequiresDecision);

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
            var toStageName = targetStage.Name;

            await _publisher.Publish(
                new ApplicationStageChangedEvent(
                    application.Id, application.JobId, jobTitle ?? string.Empty,
                    candidate.Id, application.CandidateAccountId, candidate.Email, candidate.FirstName,
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
    private readonly IPublisher _publisher;
    private readonly IJobDirectory _jobs;
    private readonly ICurrentUser _currentUser;
    private readonly IActivityLogRepository _activityLog;
    private readonly ILogger<MarkApplicationViewedHandler> _logger;

    public MarkApplicationViewedHandler(
        IApplicationsDbContext db,
        IPublisher publisher,
        IJobDirectory jobs,
        ICurrentUser currentUser,
        IActivityLogRepository activityLog,
        ILogger<MarkApplicationViewedHandler> logger)
    {
        _db = db;
        _publisher = publisher;
        _jobs = jobs;
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

        // Publish before SaveChanges, matching MoveApplicationStageHandler: the transactional
        // outbox writes the message in the same transaction as the view stamp, so both commit or
        // neither does.
        var jobTitle = await _jobs.GetJobTitleByIdAsync(application.JobId, ct);
        await _publisher.Publish(
            new ApplicationViewedEvent(
                application.Id, application.JobId, jobTitle ?? string.Empty,
                application.CandidateId, application.CandidateAccountId, application.TenantId),
            ct);

        await _db.SaveChangesAsync(ct);

        // Logged best-effort after commit; see MoveApplicationStageHandler for the rationale.
        await _activityLog.TryAddAsync(
            ApplicationActivity.Viewed(application.Id, _currentUser.UserId), _logger, ct);

        return Result.Success(true);
    }
}

// ---- MarkCvDownloaded (read receipt for the CV specifically) ----
// Fired by the API layer when a company user requests a CV download URL. Same trust model as
// MarkViewed: no client-callable "mark downloaded" endpoint, so a forged receipt cannot be
// produced. Deliberately no activity-log entry — unlike Viewed/StageChanged/Rejected this signal
// has no recruiter-facing audit surface today, only the candidate notification.
public sealed record MarkCvDownloadedCommand(Guid ApplicationId) : ICommand<bool>;

public sealed class MarkCvDownloadedHandler : ICommandHandler<MarkCvDownloadedCommand, bool>
{
    private readonly IApplicationsDbContext _db;
    private readonly IPublisher _publisher;
    private readonly IJobDirectory _jobs;

    public MarkCvDownloadedHandler(IApplicationsDbContext db, IPublisher publisher, IJobDirectory jobs)
    {
        _db = db;
        _publisher = publisher;
        _jobs = jobs;
    }

    public async Task<Result<bool>> Handle(MarkCvDownloadedCommand command, CancellationToken ct)
    {
        var application = await _db.Applications
            .FirstOrDefaultAsync(a => a.Id == command.ApplicationId, ct);
        if (application is null)
            return Result.Failure<bool>(ApplicationErrors.NotFound);

        if (!application.MarkCvDownloaded())
            return Result.Success(false);

        // Publish before SaveChanges, matching MarkApplicationViewedHandler: the transactional
        // outbox writes the message in the same transaction as the download stamp.
        var jobTitle = await _jobs.GetJobTitleByIdAsync(application.JobId, ct);
        await _publisher.Publish(
            new ApplicationCvDownloadedEvent(
                application.Id, application.JobId, jobTitle ?? string.Empty,
                application.CandidateId, application.CandidateAccountId, application.TenantId),
            ct);

        await _db.SaveChangesAsync(ct);

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

        // Rejecting also parks the application in the pipeline's FinalRejected stage, so the
        // board and the status always tell the same story. A pipeline missing that stage is a
        // data bug we tolerate by leaving the application where it is rather than blocking the
        // recruiter's decision.
        var rejectedStageId = await TerminalStageLookup.FindAsync(
            _db, application.JobId, PipelineStageType.FinalRejected, ct)
            ?? application.CurrentStageId;

        try
        {
            application.Reject(command.Reason, rejectedStageId);
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

// Resolves the id of a job pipeline's terminal stage of the given type. Shared by the reject and
// hire handlers, which both park the application in the matching terminal stage. Returns null
// when the job has no pipeline or the pipeline lacks that stage.
internal static class TerminalStageLookup
{
    internal static async Task<Guid?> FindAsync(
        IApplicationsDbContext db, Guid jobId, PipelineStageType stageType, CancellationToken ct)
    {
        return await (
            from p in db.Pipelines.AsNoTracking()
            join s in db.PipelineStages.AsNoTracking() on p.Id equals s.PipelineId
            where p.JobId == jobId && s.Type == stageType
            select (Guid?)s.Id)
            .FirstOrDefaultAsync(ct);
    }
}

// ---- Hire ----
// The positive counterpart of Reject: flips the application to its terminal success status and
// parks it in the pipeline's FinalHired stage in one operation. This is the ONLY way into that
// stage — MoveStage refuses terminal targets — so "the candidate saw 'hired'" always implies the
// application really is Hired and immutable from here on.
public sealed record HireApplicationCommand(Guid ApplicationId) : ICommand<bool>;

public sealed class HireApplicationValidator : AbstractValidator<HireApplicationCommand>
{
    public HireApplicationValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
    }
}

public sealed class HireApplicationHandler : ICommandHandler<HireApplicationCommand, bool>
{
    private readonly IApplicationsDbContext _db;
    private readonly IPublisher _publisher;
    private readonly IJobDirectory _jobs;
    private readonly ICurrentUser _currentUser;
    private readonly IActivityLogRepository _activityLog;
    private readonly ILogger<HireApplicationHandler> _logger;

    public HireApplicationHandler(
        IApplicationsDbContext db,
        IPublisher publisher,
        IJobDirectory jobs,
        ICurrentUser currentUser,
        IActivityLogRepository activityLog,
        ILogger<HireApplicationHandler> logger)
    {
        _db = db;
        _publisher = publisher;
        _jobs = jobs;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(HireApplicationCommand command, CancellationToken ct)
    {
        var application = await _db.Applications
            .FirstOrDefaultAsync(a => a.Id == command.ApplicationId, ct);
        if (application is null)
            return Result.Failure<bool>(ApplicationErrors.NotFound);

        // Same fallback rule as Reject: a pipeline without a FinalHired stage is a data bug we
        // tolerate by leaving the application in place rather than blocking the decision.
        var hiredStageId = await TerminalStageLookup.FindAsync(
            _db, application.JobId, PipelineStageType.FinalHired, ct)
            ?? application.CurrentStageId;

        try
        {
            application.Hire(hiredStageId);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<bool>(ApplicationErrors.InvalidOperation(ex.Message));
        }

        // Mirrors RejectApplicationHandler: gather what the candidate's congratulation email
        // needs, publish BEFORE SaveChanges so the transactional outbox commits the message
        // atomically with the hired status.
        var candidate = await _db.Candidates.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == application.CandidateId, ct);
        if (candidate is not null)
        {
            var jobTitle = await _jobs.GetJobTitleByIdAsync(application.JobId, ct);
            await _publisher.Publish(
                new ApplicationHiredEvent(
                    application.Id, application.JobId, jobTitle ?? string.Empty,
                    candidate.Id, candidate.Email, candidate.FirstName, application.TenantId),
                ct);
        }

        await _db.SaveChangesAsync(ct);

        // Logged best-effort after commit; see MoveApplicationStageHandler for the rationale.
        await _activityLog.TryAddAsync(
            ApplicationActivity.Hired(application.Id, _currentUser.UserId), _logger, ct);

        return Result.Success(true);
    }
}
