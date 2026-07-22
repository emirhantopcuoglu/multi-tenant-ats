using Ats.Modules.Notifications.Domain;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Notifications.Infrastructure;

// Writes an in-app notification row the first time a company user downloads an application's CV —
// the "cv indirildi" signal from roadmap 3.2. Own queue, same shape as
// ApplicationViewedNotificationConsumer: skip messages with no candidate account behind them, and
// guard the write with the message-id idempotency key so an at-least-once redelivery can't double
// the row.
public sealed class ApplicationCvDownloadedNotificationConsumer
    : IConsumer<ApplicationCvDownloadedIntegrationEvent>
{
    private readonly NotificationsDbContext _db;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly ILogger<ApplicationCvDownloadedNotificationConsumer> _logger;

    public ApplicationCvDownloadedNotificationConsumer(
        NotificationsDbContext db,
        IIdempotencyGuard idempotencyGuard,
        ILogger<ApplicationCvDownloadedNotificationConsumer> logger)
    {
        _db = db;
        _idempotencyGuard = idempotencyGuard;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ApplicationCvDownloadedIntegrationEvent> context)
    {
        var message = context.Message;

        var notification = TryBuildNotification(message);
        if (notification is null)
        {
            _logger.LogDebug(
                "Skipped in-app cv-downloaded notification for application {ApplicationId}: " +
                "no candidate account behind it",
                message.ApplicationId);
            return;
        }

        var key = $"notifications:in-app:application-cv-downloaded:{context.MessageId}";
        var written = await _idempotencyGuard.ProcessOnceAsync(key, async () =>
        {
            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync(context.CancellationToken);
        });

        if (!written)
        {
            _logger.LogInformation(
                "Skipped duplicate in-app cv-downloaded notification for application {ApplicationId}",
                message.ApplicationId);
            return;
        }

        _logger.LogInformation(
            "Wrote in-app cv-downloaded notification for application {ApplicationId} " +
            "to candidate account {CandidateAccountId}",
            message.ApplicationId,
            message.CandidateAccountId);
    }

    // The message-to-row mapping, kept static and side-effect free so tests can pin the payload
    // contents and the no-account skip without standing up a ConsumeContext.
    public static Notification? TryBuildNotification(ApplicationCvDownloadedIntegrationEvent message)
    {
        if (message.CandidateAccountId is not { } candidateAccountId)
            return null;

        var payload = new NotificationPayloads.ApplicationCvDownloaded(
            message.ApplicationId, message.JobTitle);

        return Notification.ForCandidate(
            candidateAccountId,
            NotificationType.ApplicationCvDownloaded,
            NotificationPayloads.Serialize(payload));
    }
}
