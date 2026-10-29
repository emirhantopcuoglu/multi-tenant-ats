using Ats.Modules.Notifications.Domain;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Notifications.Infrastructure;

// In-app row when an application moves stage. Applications without a candidate account are
// skipped: there is no feed to deliver to. Idempotent on the message id.
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

    // Static so tests can check the mapping without a ConsumeContext.
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
