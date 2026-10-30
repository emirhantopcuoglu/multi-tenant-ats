using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Application.Applications;
using Ats.Modules.Applications.Domain;
using Ats.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MassTransit;

using ApplicationEntity = Ats.Modules.Applications.Domain.Application;

namespace Ats.Modules.Applications.Infrastructure;

// Moves the application to the Interview stage when an interview is scheduled. Forward only: an
// application already at or past that stage stays put. Announced like any stage change. No
// ambient tenant, so queries bypass the filter and match the event's TenantId.
public sealed class AdvanceToInterviewStageConsumer : IConsumer<InterviewScheduledIntegrationEvent>
{
    private readonly IApplicationsDbContext _db;
    private readonly IActivityLogRepository _activityLog;
    private readonly ILogger<AdvanceToInterviewStageConsumer> _logger;

    public AdvanceToInterviewStageConsumer(
        IApplicationsDbContext db, IActivityLogRepository activityLog,
        ILogger<AdvanceToInterviewStageConsumer> logger)
    {
        _db = db;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<InterviewScheduledIntegrationEvent> context)
    {
        var message = context.Message;
        var move = await AdvanceAsync(message.ApplicationId, message.TenantId, context.CancellationToken);
        if (move is null)
            return;

        // Published through ConsumeContext so the consumer outbox (see the definition) commits it with
        // the stage move.
        await context.Publish(
            new ApplicationStageChangedIntegrationEvent(
                message.ApplicationId, message.JobId, message.JobTitle,
                message.CandidateId, message.CandidateAccountId,
                message.CandidateEmail, message.CandidateFirstName,
                move.FromStageId, move.FromStageName, move.ToStageId, move.ToStageName,
                message.TenantId),
            context.CancellationToken);
    }

    public sealed record StageMove(
        Guid FromStageId, string FromStageName, Guid ToStageId, string ToStageName);

    // Split out from Consume so integration tests can call it without a ConsumeContext.
    public async Task<StageMove?> AdvanceAsync(
        Guid applicationId, Guid tenantId, CancellationToken cancellationToken)
    {
        var application = await _db.Applications
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                a => a.Id == applicationId && a.TenantId == tenantId, cancellationToken);

        // The application may have changed since the event was published.
        if (application is null || application.Status != ApplicationStatus.Active)
            return null;

        var pipeline = await _db.Pipelines
            .IgnoreQueryFilters()
            .Include(p => p.Stages)
            .FirstOrDefaultAsync(
                p => p.JobId == application.JobId && p.TenantId == tenantId && !p.IsDeleted,
                cancellationToken);

        var target = pipeline is null
            ? null
            : InterviewStageAdvancement.FindTarget(application.Status, application.CurrentStageId, pipeline.Stages);
        if (target is null)
            return null;

        var fromStageId = application.CurrentStageId;
        var fromStageName = pipeline!.Stages.FirstOrDefault(s => s.Id == fromStageId)?.Name ?? string.Empty;

        try
        {
            application.MoveToStage(target.Id);
        }
        catch (InvalidOperationException)
        {
            return null;
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Explicit tenant: there is no ambient one in a consumer.
        await _activityLog.TryAddAsync(
            ApplicationActivity.StageChanged(application.Id, actorUserId: null, fromStageId, target.Id),
            tenantId, _logger, cancellationToken);

        return new StageMove(fromStageId, fromStageName, target.Id, target.Name);
    }
}
