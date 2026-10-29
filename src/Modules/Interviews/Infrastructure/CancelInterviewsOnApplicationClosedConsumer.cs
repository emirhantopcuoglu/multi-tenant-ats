using Ats.Modules.Interviews.Application;
using Ats.Modules.Interviews.Domain;
using Ats.Shared.Contracts.Applications;
using Ats.Shared.Contracts.Notifications;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Interviews.Infrastructure;

// Cancels an application's upcoming interviews when it is rejected or withdrawn. Interviews that
// already started are left alone. The candidate isn't emailed: they already know the application
// is closed. No ambient tenant, so the tenant comes from the message.
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

    // One overload per message so the reason mapping can be tested without a ConsumeContext.
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

    // Returns the number cancelled, for tests.
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
            // The start time may pass between the query and here; the guard refusing then is fine.
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
