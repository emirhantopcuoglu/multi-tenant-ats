using Ats.Modules.Notifications.Domain;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Notifications.Infrastructure;

// In-app row the first time a company user opens the application. Skips applications without a
// candidate account; the message id guards against duplicates.
public sealed class ApplicationViewedNotificationConsumer
    : IConsumer<ApplicationViewedIntegrationEvent>
{
    private readonly NotificationsDbContext _db;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly ILogger<ApplicationViewedNotificationConsumer> _logger;

    public ApplicationViewedNotificationConsumer(
        NotificationsDbContext db,
        IIdempotencyGuard idempotencyGuard,
        ILogger<ApplicationViewedNotificationConsumer> logger)
    {
        _db = db;
        _idempotencyGuard = idempotencyGuard;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ApplicationViewedIntegrationEvent> context)
    {
        var message = context.Message;

        var notification = TryBuildNotification(message);
        if (notification is null)
        {
            _logger.LogDebug(
                "Skipped in-app viewed notification for application {ApplicationId}: " +
                "no candidate account behind it",
                message.ApplicationId);
            return;
        }

        var key = $"notifications:in-app:application-viewed:{context.MessageId}";
        var written = await _idempotencyGuard.ProcessOnceAsync(key, async () =>
        {
            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync(context.CancellationToken);
        });

        if (!written)
        {
            _logger.LogInformation(
                "Skipped duplicate in-app viewed notification for application {ApplicationId}",
                message.ApplicationId);
            return;
        }

        _logger.LogInformation(
            "Wrote in-app viewed notification for application {ApplicationId} " +
            "to candidate account {CandidateAccountId}",
            message.ApplicationId,
            message.CandidateAccountId);
    }

    // Static so tests can check the mapping without a ConsumeContext.
    public static Notification? TryBuildNotification(ApplicationViewedIntegrationEvent message)
    {
        if (message.CandidateAccountId is not { } candidateAccountId)
            return null;

        var payload = new NotificationPayloads.ApplicationViewed(message.ApplicationId, message.JobTitle);

        return Notification.ForCandidate(
            candidateAccountId,
            NotificationType.ApplicationViewed,
            NotificationPayloads.Serialize(payload));
    }
}
