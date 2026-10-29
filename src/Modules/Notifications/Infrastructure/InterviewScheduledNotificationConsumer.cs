using Ats.Modules.Notifications.Domain;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Notifications.Infrastructure;

public sealed class InterviewScheduledNotificationConsumer
    : IConsumer<InterviewScheduledIntegrationEvent>
{
    private readonly NotificationsDbContext _db;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly ILogger<InterviewScheduledNotificationConsumer> _logger;

    public InterviewScheduledNotificationConsumer(
        NotificationsDbContext db,
        IIdempotencyGuard idempotencyGuard,
        ILogger<InterviewScheduledNotificationConsumer> logger)
    {
        _db = db;
        _idempotencyGuard = idempotencyGuard;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<InterviewScheduledIntegrationEvent> context)
    {
        var message = context.Message;

        var notification = TryBuildNotification(message);
        if (notification is null)
        {
            _logger.LogDebug(
                "Skipped in-app interview-scheduled notification for interview {InterviewId}: " +
                "no candidate account behind the application",
                message.InterviewId);
            return;
        }

        var key = $"notifications:in-app:interview-scheduled:{context.MessageId}";
        var written = await _idempotencyGuard.ProcessOnceAsync(key, async () =>
        {
            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync(context.CancellationToken);
        });

        if (!written)
        {
            _logger.LogInformation(
                "Skipped duplicate in-app interview-scheduled notification for interview {InterviewId}",
                message.InterviewId);
            return;
        }

        _logger.LogInformation(
            "Wrote in-app interview-scheduled notification for interview {InterviewId} " +
            "to candidate account {CandidateAccountId}",
            message.InterviewId,
            message.CandidateAccountId);
    }

    // Static so tests can check the mapping without a ConsumeContext.
    public static Notification? TryBuildNotification(InterviewScheduledIntegrationEvent message)
    {
        if (message.CandidateAccountId is not { } candidateAccountId)
            return null;

        var payload = new NotificationPayloads.InterviewScheduled(
            message.ApplicationId, message.JobTitle, message.InterviewType,
            message.ScheduledAtUtc, message.DurationMinutes, message.RoomToken);

        return Notification.ForCandidate(
            candidateAccountId,
            NotificationType.InterviewScheduled,
            NotificationPayloads.Serialize(payload));
    }
}
