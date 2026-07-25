using Ats.Modules.Interviews.Application;
using Ats.Modules.Interviews.Domain;
using Ats.Shared.Contracts.Notifications;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Interviews.Infrastructure;

// Rejecting an application used to leave its booked interviews on the calendar: interviewers kept
// the slot, the conflict guard kept treating it as occupied, and the candidate kept an invitation
// for a meeting nobody intended to hold.
//
// Only interviews that have not started are cancelled. One that already happened is a fact —
// rejecting the application afterwards does not un-hold it, and an elapsed interview still needs an
// honest outcome recorded.
//
// Silent towards the candidate on purpose: they are already being emailed that the application was
// rejected, and "your interview is cancelled" on top of it adds nothing they do not know. The
// cancellation is still visible in their interview list.
//
// Runs with no ambient tenant, like AdvanceToInterviewStageConsumer, so every query bypasses the
// global filter and matches TenantId from the message explicitly.
public sealed class CancelInterviewsOnRejectionConsumer
    : IConsumer<ApplicationRejectedIntegrationEvent>
{
    private readonly IInterviewsDbContext _db;
    private readonly ILogger<CancelInterviewsOnRejectionConsumer> _logger;

    public CancelInterviewsOnRejectionConsumer(
        IInterviewsDbContext db, ILogger<CancelInterviewsOnRejectionConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task Consume(ConsumeContext<ApplicationRejectedIntegrationEvent> context) =>
        CancelAsync(context.Message.ApplicationId, context.Message.TenantId, context.CancellationToken);

    // Split from Consume so it is testable without a fake ConsumeContext, matching
    // AdvanceToInterviewStageConsumer. Returns how many were cancelled, for the same reason.
    public async Task<int> CancelAsync(
        Guid applicationId, Guid tenantId, CancellationToken cancellationToken)
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
                interview.Cancel(InterviewCancellationReason.ApplicationRejected, note: null, nowUtc);
            }
            catch (InvalidOperationException)
            {
                continue;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Cancelled {Count} upcoming interview(s) for rejected application {ApplicationId}",
            scheduled.Count, applicationId);

        return scheduled.Count;
    }
}
