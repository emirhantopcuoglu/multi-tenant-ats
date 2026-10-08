using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Application.Applications;
using Ats.Modules.Applications.Domain;
using Ats.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MassTransit;

using ApplicationEntity = Ats.Modules.Applications.Domain.Application;

namespace Ats.Modules.Applications.Infrastructure;

// Keeps the pipeline honest with the interview calendar: when a recruiter schedules an interview,
// the application should already be sitting in the funnel's Interview stage rather than left
// behind in an earlier one while the calendar says otherwise.
//
// Forward-only: only advances an application into the Interview stage, and only if it is not
// already there or past it (e.g. already in Offer). A follow-up interview scheduled later in the
// process must never pull the application backwards.
//
// The move is announced like any other stage change — activity log plus
// ApplicationStageChangedIntegrationEvent. It used to be silent, which meant the candidate's
// tracking timeline skipped the step entirely and jumped from screening to "Interview, in review"
// with no date.
//
// Runs with no ambient tenant — a message consumer has no resolved ICurrentTenant, the same
// reasoning as CvParsingConsumer and IJobDirectory.GetJobRequirementsAsync — so every query bypasses
// the global filter and checks TenantId explicitly against the event's own TenantId.
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

        // Published from the ConsumeContext, not the scoped IPublishEndpoint: the bus outbox in
        // Program.cs is request-scoped, while a consumer's publish is captured by the endpoint's own
        // outbox filter — see AdvanceToInterviewStageConsumerDefinition. That filter is what puts
        // this write in the same transaction as AdvanceAsync's SaveChangesAsync, so a broker outage
        // can no longer leave the stage moved with the announcement lost.
        //
        // Stage names come from the pipeline loaded below, candidate contact from the message we
        // are reacting to.
        await context.Publish(
            new ApplicationStageChangedIntegrationEvent(
                message.ApplicationId, message.JobId, message.JobTitle,
                message.CandidateId, message.CandidateAccountId,
                message.CandidateEmail, message.CandidateFirstName,
                move.FromStageId, move.FromStageName, move.ToStageId, move.ToStageName,
                message.TenantId),
            context.CancellationToken);
    }

    // Describes a move that actually happened, so Consume knows whether to announce it.
    public sealed record StageMove(
        Guid FromStageId, string FromStageName, Guid ToStageId, string ToStageName);

    // The DB-touching core, split out from Consume so it can be exercised directly in an
    // integration test without standing up a fake MassTransit ConsumeContext. Public (rather than
    // internal) specifically to avoid needing InternalsVisibleTo just for testing.
    public async Task<StageMove?> AdvanceAsync(
        Guid applicationId, Guid tenantId, CancellationToken cancellationToken)
    {
        var application = await _db.Applications
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                a => a.Id == applicationId && a.TenantId == tenantId, cancellationToken);

        // The event only fires once ScheduleInterviewHandler has already confirmed the application
        // exists and is active, but a consumer must never assume delivery order or a stable world
        // between publish and consume — a status flip in between (e.g. a concurrent rejection) is
        // a legitimate reason to do nothing here.
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
            // The application turned terminal between the status check above and here — nothing
            // left to advance.
            return null;
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Best-effort, logged-and-swallowed on failure — see ActivityLogRepositoryExtensions: an
        // append-only history line must never fail or roll back a stage move that already happened.
        // The tenant is passed explicitly: without an ambient one the request-scoped overload throws,
        // which is why this line silently wrote nothing and the candidate's timeline lost the step.
        await _activityLog.TryAddAsync(
            ApplicationActivity.StageChanged(application.Id, actorUserId: null, fromStageId, target.Id),
            tenantId, _logger, cancellationToken);

        return new StageMove(fromStageId, fromStageName, target.Id, target.Name);
    }
}
