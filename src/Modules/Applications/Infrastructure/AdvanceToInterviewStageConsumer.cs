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
// behind in an earlier one while the calendar says otherwise (Faz 4.2 — the pipeline and interview
// scheduling used to be entirely disconnected).
//
// Forward-only and silent by design:
//   - Forward-only: only advances an application into the Interview stage, and only if it is not
//     already there or past it (e.g. already in Offer). A follow-up interview scheduled later in
//     the process must never pull the application backwards.
//   - Silent: this does NOT publish an ApplicationStageChangedEvent. The candidate already gets an
//     interview-scheduled notification from InterviewScheduledNotificationConsumer/EmailConsumer;
//     a second "your stage changed" notification for the same action would be redundant noise. The
//     move is still recorded to the activity log, so both the recruiter's and the candidate's
//     timelines show it honestly (Faz 4.2 depends on the candidate timeline reflecting reality).
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

    public Task Consume(ConsumeContext<InterviewScheduledIntegrationEvent> context) =>
        AdvanceAsync(context.Message.ApplicationId, context.Message.TenantId, context.CancellationToken);

    // The DB-touching core, split out from Consume so it can be exercised directly in an
    // integration test without standing up a fake MassTransit ConsumeContext. Public (rather than
    // internal) specifically to avoid needing InternalsVisibleTo just for testing.
    public async Task AdvanceAsync(Guid applicationId, Guid tenantId, CancellationToken cancellationToken)
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
            return;

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
            return;

        var fromStageId = application.CurrentStageId;

        try
        {
            application.MoveToStage(target.Id);
        }
        catch (InvalidOperationException)
        {
            // The application turned terminal between the status check above and here — nothing
            // left to advance.
            return;
        }

        await _db.SaveChangesAsync(cancellationToken);

        // Best-effort, logged-and-swallowed on failure — see ActivityLogRepositoryExtensions: an
        // append-only history line must never fail or roll back a stage move that already happened.
        await _activityLog.TryAddAsync(
            ApplicationActivity.StageChanged(application.Id, actorUserId: null, fromStageId, target.Id),
            _logger, cancellationToken);
    }
}
