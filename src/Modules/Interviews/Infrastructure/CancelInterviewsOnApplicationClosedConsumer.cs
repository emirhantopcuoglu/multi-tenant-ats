using Ats.Modules.Interviews.Application;
using Ats.Modules.Interviews.Domain;
using Ats.Shared.Contracts.Applications;
using Ats.Shared.Contracts.Notifications;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Interviews.Infrastructure;

// An application reaching a terminal status used to leave its booked interviews on the calendar:
// interviewers kept the slot, the conflict guard kept treating it as occupied, and the candidate kept
// an invitation for a meeting nobody intended to hold.
//
// One consumer for both ways that happens — the company rejects, or the candidate withdraws — because
// the work is identical and only the recorded reason differs. Two classes would have been the same
// twenty lines twice, and the second copy is where the fix for the first one gets forgotten.
//
// Only interviews that have not started are cancelled. One that already happened is a fact — closing
// the application afterwards does not un-hold it, and an elapsed interview still needs an honest
// outcome recorded.
//
// Silent towards the candidate on purpose. On rejection they are already being emailed that the
// application was rejected; on withdrawal they are the one who closed it. Either way "your interview
// is cancelled" on top adds nothing they do not know. The cancellation is still visible in their
// interview list.
//
// Runs with no ambient tenant, like AdvanceToInterviewStageConsumer, so every query bypasses the
// global filter and matches TenantId from the message explicitly.
public sealed class CancelInterviewsOnApplicationClosedConsumer
    : IConsumer<ApplicationRejectedIntegrationEvent>,
      IConsumer<ApplicationWithdrawnIntegrationEvent>
{
    private readonly IInterviewsDbContext _db;
    private readonly ILogger<CancelInterviewsOnApplicationClosedConsumer> _logger;

    public CancelInterviewsOnApplicationClosedConsumer(
        IInterviewsDbContext db, ILogger<CancelInterviewsOnApplicationClosedConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task Consume(ConsumeContext<ApplicationRejectedIntegrationEvent> context) =>
        CancelAsync(context.Message, context.CancellationToken);

    public Task Consume(ConsumeContext<ApplicationWithdrawnIntegrationEvent> context) =>
        CancelAsync(context.Message, context.CancellationToken);

    // One typed overload per message, holding the only thing that differs between them: which reason
    // gets recorded. They exist so that mapping is assertable — the Consume methods above take a
    // ConsumeContext, which cannot be built without a bus, so anything decided inside them is
    // untestable in practice. Keeping them as a bare unwrap of Message/CancellationToken leaves
    // nothing in them worth testing.
    public Task<int> CancelAsync(
        ApplicationRejectedIntegrationEvent message, CancellationToken cancellationToken) =>
        CancelAsync(
            message.ApplicationId, message.TenantId,
            InterviewCancellationReason.ApplicationRejected, cancellationToken);

    public Task<int> CancelAsync(
        ApplicationWithdrawnIntegrationEvent message, CancellationToken cancellationToken) =>
        CancelAsync(
            message.ApplicationId, message.TenantId,
            InterviewCancellationReason.CandidateWithdrew, cancellationToken);

    // The shared work, reason supplied by the overloads above. Returns how many were cancelled so
    // tests can distinguish "nothing to do" from "did the work", matching AdvanceToInterviewStageConsumer.
    public async Task<int> CancelAsync(
        Guid applicationId,
        Guid tenantId,
        InterviewCancellationReason reason,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;

        var scheduled = await _db.Interviews
            .IgnoreQueryFilters()
            .Where(i => i.ApplicationId == applicationId
                        && i.TenantId == tenantId
                        && !i.IsDeleted
                        && i.Status == InterviewStatus.Scheduled
                        && i.ScheduledAtUtc > nowUtc)
            .ToListAsync(cancellationToken);

        if (scheduled.Count == 0)
            return 0;

        foreach (var interview in scheduled)
        {
            // The clock can cross a start time between the query and here; the guard would then
            // refuse, and that refusal is correct rather than an error worth failing the message for.
            try
            {
                interview.Cancel(reason, note: null, nowUtc);
            }
            catch (InvalidOperationException)
            {
                continue;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Cancelled {Count} upcoming interview(s) for closed application {ApplicationId} ({Reason})",
            scheduled.Count, applicationId, reason);

        return scheduled.Count;
    }
}
