using Ats.Modules.Notifications.Domain;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Notifications.Infrastructure;

// Writes an in-app notification when an interview is called off. Same shape as its sibling
// consumers: skip without a candidate account, idempotency guard on the message id.
public sealed class InterviewCancelledNotificationConsumer
    : IConsumer<InterviewCancelledIntegrationEvent>
{
    private readonly NotificationsDbContext _db;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly ILogger<InterviewCancelledNotificationConsumer> _logger;

    public InterviewCancelledNotificationConsumer(
        NotificationsDbContext db,
        IIdempotencyGuard idempotencyGuard,
        ILogger<InterviewCancelledNotificationConsumer> logger)
    {
        _db = db;
        _idempotencyGuard = idempotencyGuard;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<InterviewCancelledIntegrationEvent> context)
    {
        var message = context.Message;

        var notification = TryBuildNotification(message);
        if (notification is null)
        {
            _logger.LogDebug(
                "Skipped in-app interview-cancelled notification for interview {InterviewId}: " +
                "no candidate account behind the application",
                message.InterviewId);
            return;
        }

        var key = $"notifications:in-app:interview-cancelled:{context.MessageId}";
        var written = await _idempotencyGuard.ProcessOnceAsync(key, async () =>
        {
            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync(context.CancellationToken);
        });

        if (!written)
        {
            _logger.LogInformation(
                "Skipped duplicate in-app interview-cancelled notification for interview {InterviewId}",
                message.InterviewId);
            return;
        }

        _logger.LogInformation(
            "Wrote in-app interview-cancelled notification for interview {InterviewId} " +
            "to candidate account {CandidateAccountId}",
            message.InterviewId,
            message.CandidateAccountId);
    }

    public static Notification? TryBuildNotification(InterviewCancelledIntegrationEvent message)
    {
        if (message.CandidateAccountId is not { } candidateAccountId)
            return null;

        var payload = new NotificationPayloads.InterviewCancelled(
            message.ApplicationId, message.JobTitle, message.InterviewType,
            message.ScheduledAtUtc, message.Reason);

        return Notification.ForCandidate(
            candidateAccountId,
            NotificationType.InterviewCancelled,
            NotificationPayloads.Serialize(payload));
    }
}
