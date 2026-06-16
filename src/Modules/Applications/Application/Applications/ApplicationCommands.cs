using Ats.Modules.Applications.Application.Events;
using Ats.Shared.Kernel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

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

    public MoveApplicationStageHandler(IApplicationsDbContext db, IPublisher publisher)
    {
        _db = db;
        _publisher = publisher;
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
    public RejectApplicationHandler(IApplicationsDbContext db) => _db = db;

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
        return Result.Success(true);
    }
}
