using Ats.Modules.Applications.Application.Events;
using Ats.Modules.Applications.Domain;
using Ats.Shared.Contracts.Jobs;
using Ats.Shared.Kernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Applications.Application.Applications;

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

        // The target stage must belong to this job's pipeline; the entity can't check that.
        var pipeline = await _db.Pipelines
            .Include(p => p.Stages)
            .FirstOrDefaultAsync(p => p.JobId == application.JobId, ct);
        if (pipeline is null)
            return Result.Failure<bool>(ApplicationErrors.StageNotInPipeline);

        var targetStage = pipeline.Stages.FirstOrDefault(s => s.Id == command.TargetStageId);
        if (targetStage is null)
            return Result.Failure<bool>(ApplicationErrors.StageNotInPipeline);

        // Terminal stages also change the status, which only hire and reject do.
        if (targetStage.Type is PipelineStageType.FinalHired or PipelineStageType.FinalRejected)
            return Result.Failure<bool>(ApplicationErrors.TerminalStageRequiresDecision);

        // Forward only. Moving back is CorrectApplicationStageCommand, so a misclick never looks like
        // progress or sends the candidate a second notification.
        var currentStage = pipeline.Stages.FirstOrDefault(s => s.Id == application.CurrentStageId);
        if (currentStage is not null && targetStage.Order <= currentStage.Order)
            return Result.Failure<bool>(ApplicationErrors.CannotMoveBackward);

        var fromStageId = application.CurrentStageId;

        try
        {
            application.MoveToStage(command.TargetStageId);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<bool>(ApplicationErrors.InvalidOperation(ex.Message));
        }

        // Published before SaveChanges so the outbox message commits with the stage move.
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
        else
        {
            MissingCandidateLog.Warn(
                _logger, "moved to a new stage", application.Id, application.CandidateId);
        }

        await _db.SaveChangesAsync(ct);

        // After the save; the activity log is in MongoDB, outside the transaction.
        await _activityLog.TryAddAsync(
            ApplicationActivity.StageChanged(
                application.Id, _currentUser.UserId, fromStageId, command.TargetStageId),
            _logger, ct);

        return Result.Success(true);
    }
}

// The only way to move an application backward. Separate from MoveStage and needs a reason.
public sealed record CorrectApplicationStageCommand(Guid ApplicationId, Guid TargetStageId, string Reason)
    : ICommand<bool>;

public sealed class CorrectApplicationStageValidator : AbstractValidator<CorrectApplicationStageCommand>
{
    public CorrectApplicationStageValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
        RuleFor(x => x.TargetStageId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
    }
}

public sealed class CorrectApplicationStageHandler : ICommandHandler<CorrectApplicationStageCommand, bool>
{
    private readonly IApplicationsDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IActivityLogRepository _activityLog;
    private readonly ILogger<CorrectApplicationStageHandler> _logger;

    public CorrectApplicationStageHandler(
        IApplicationsDbContext db,
        ICurrentUser currentUser,
        IActivityLogRepository activityLog,
        ILogger<CorrectApplicationStageHandler> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(CorrectApplicationStageCommand command, CancellationToken ct)
    {
        var application = await _db.Applications
            .FirstOrDefaultAsync(a => a.Id == command.ApplicationId, ct);
        if (application is null)
            return Result.Failure<bool>(ApplicationErrors.NotFound);

        // Same checks as MoveStage, except a correction may go in either direction.
        var pipeline = await _db.Pipelines
            .Include(p => p.Stages)
            .FirstOrDefaultAsync(p => p.JobId == application.JobId, ct);
        if (pipeline is null)
            return Result.Failure<bool>(ApplicationErrors.StageNotInPipeline);

        var targetStage = pipeline.Stages.FirstOrDefault(s => s.Id == command.TargetStageId);
        if (targetStage is null)
            return Result.Failure<bool>(ApplicationErrors.StageNotInPipeline);

        if (targetStage.Type is PipelineStageType.FinalHired or PipelineStageType.FinalRejected)
            return Result.Failure<bool>(ApplicationErrors.TerminalStageRequiresDecision);

        var fromStageId = application.CurrentStageId;
        if (fromStageId == command.TargetStageId)
            return Result.Failure<bool>(ApplicationErrors.CannotMoveBackward);

        try
        {
            application.MoveToStage(command.TargetStageId);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<bool>(ApplicationErrors.InvalidOperation(ex.Message));
        }

        // No stage-changed event: a correction isn't progress, so the candidate isn't notified.
        await _db.SaveChangesAsync(ct);

        // Logged with the reason so the history explains the backward move.
        await _activityLog.TryAddAsync(
            ApplicationActivity.StageCorrected(
                application.Id, _currentUser.UserId, fromStageId, command.TargetStageId, command.Reason),
            _logger, ct);

        return Result.Success(true);
    }
}

// Sent by the API when a company user opens the application; there is no client endpoint for it,
// so it can't be faked. Only the first view counts. A concurrent double view may log twice; the
// candidate view reads only the earliest.
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

        // Published before SaveChanges so the outbox message commits with the view stamp.
        var jobTitle = await _jobs.GetJobTitleByIdAsync(application.JobId, ct);
        await _publisher.Publish(
            new ApplicationViewedEvent(
                application.Id, application.JobId, jobTitle ?? string.Empty,
                application.CandidateId, application.CandidateAccountId, application.TenantId),
            ct);

        await _db.SaveChangesAsync(ct);

        await _activityLog.TryAddAsync(
            ApplicationActivity.Viewed(application.Id, _currentUser.UserId), _logger, ct);

        return Result.Success(true);
    }
}

// Sent by the API when a company user requests the CV link. No activity log entry, only the
// candidate notification.
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

        // Published before SaveChanges so the outbox message commits with the download stamp.
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

        // Also moves the application to FinalRejected. If the pipeline has no such stage, it stays put
        // rather than blocking the decision.
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

        // The internal reason is never passed on.
        var candidate = await _db.Candidates.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == application.CandidateId, ct);
        if (candidate is not null)
        {
            var jobTitle = await _jobs.GetJobTitleByIdAsync(application.JobId, ct);
            // Published before saving so the outbox message commits with the status change.
            await _publisher.Publish(
                new ApplicationRejectedEvent(
                    application.Id, application.JobId, jobTitle ?? string.Empty,
                    candidate.Id, candidate.Email, candidate.FirstName, application.TenantId),
                ct);
        }
        else
        {
            MissingCandidateLog.Warn(_logger, "rejected", application.Id, application.CandidateId);
        }

        await _db.SaveChangesAsync(ct);

        await _activityLog.TryAddAsync(
            ApplicationActivity.Rejected(application.Id, _currentUser.UserId, command.Reason),
            _logger, ct);

        return Result.Success(true);
    }
}

// Logs when a notification is skipped because the candidate row is gone (Candidate is
// soft-deletable). The decision still stands; this makes "I never heard back" traceable.
internal static class MissingCandidateLog
{
    internal static void Warn(ILogger logger, string action, Guid applicationId, Guid candidateId) =>
        logger.LogWarning(
            "Application {ApplicationId} was {Action} but candidate {CandidateId} no longer exists; " +
            "the candidate was not notified",
            applicationId, action, candidateId);
}

// Null when the job has no pipeline or the stage is missing.
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

// The only way into FinalHired, since MoveStage refuses terminal stages.
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

        // Same fallback as Reject when the stage is missing.
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

        // Published before SaveChanges so the outbox message commits with the hired status.
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
        else
        {
            MissingCandidateLog.Warn(_logger, "hired", application.Id, application.CandidateId);
        }

        await _db.SaveChangesAsync(ct);

        await _activityLog.TryAddAsync(
            ApplicationActivity.Hired(application.Id, _currentUser.UserId), _logger, ct);

        return Result.Success(true);
    }
}
