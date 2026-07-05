using Ats.Modules.Notifications.Domain;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Notifications.Infrastructure;

// Writes an in-app notification row when a recruiter moves an application to a new stage — the
// second subscriber to this event alongside the (future) stage-change email; each consumer gets
// its own queue, so they succeed and retry independently.
//
// The row is keyed to the candidate's global marketplace account. Messages without one (legacy
// applications submitted before candidate accounts) are skipped, not failed: there is no feed to
// deliver to, and retrying would never change that.
//
// The write is wrapped in the idempotency guard keyed on the message id, matching the email
// consumers: an at-least-once redelivery must not put the same notification in the feed twice.
public sealed class ApplicationStageChangedNotificationConsumer
    : IConsumer<ApplicationStageChangedIntegrationEvent>
{
    private readonly NotificationsDbContext _db;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly ILogger<ApplicationStageChangedNotificationConsumer> _logger;

    public ApplicationStageChangedNotificationConsumer(
        NotificationsDbContext db,
        IIdempotencyGuard idempotencyGuard,
        ILogger<ApplicationStageChangedNotificationConsumer> logger)
    {
        _db = db;
        _idempotencyGuard = idempotencyGuard;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ApplicationStageChangedIntegrationEvent> context)
    {
        var message = context.Message;

        var notification = TryBuildNotification(message);
        if (notification is null)
        {
            _logger.LogDebug(
                "Skipped in-app stage-changed notification for application {ApplicationId}: " +
                "no candidate account behind it",
                message.ApplicationId);
            return;
        }

        var key = $"notifications:in-app:stage-changed:{context.MessageId}";
        var written = await _idempotencyGuard.ProcessOnceAsync(key, async () =>
        {
            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync(context.CancellationToken);
        });

        if (!written)
        {
            _logger.LogInformation(
                "Skipped duplicate in-app stage-changed notification for application {ApplicationId}",
                message.ApplicationId);
            return;
        }

        _logger.LogInformation(
            "Wrote in-app stage-changed notification for application {ApplicationId} " +
            "to candidate account {CandidateAccountId}",
            message.ApplicationId,
            message.CandidateAccountId);
    }

    // The message-to-row mapping, kept static and side-effect free so tests can pin the payload
    // contents and the no-account skip without standing up a ConsumeContext.
    public static Notification? TryBuildNotification(ApplicationStageChangedIntegrationEvent message)
    {
        if (message.CandidateAccountId is not { } candidateAccountId)
            return null;

        var payload = new NotificationPayloads.ApplicationStageChanged(
            message.ApplicationId, message.JobTitle, message.FromStageName, message.ToStageName);

        return Notification.ForCandidate(
            candidateAccountId,
            NotificationType.ApplicationStageChanged,
            NotificationPayloads.Serialize(payload));
    }
}
