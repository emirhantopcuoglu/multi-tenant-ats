using Ats.Modules.Notifications.Domain;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Notifications.Infrastructure;

public sealed class InterviewRescheduledNotificationConsumer
    : IConsumer<InterviewRescheduledIntegrationEvent>
{
    private readonly NotificationsDbContext _db;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly ILogger<InterviewRescheduledNotificationConsumer> _logger;

    public InterviewRescheduledNotificationConsumer(
        NotificationsDbContext db,
        IIdempotencyGuard idempotencyGuard,
        ILogger<InterviewRescheduledNotificationConsumer> logger)
    {
        _db = db;
        _idempotencyGuard = idempotencyGuard;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<InterviewRescheduledIntegrationEvent> context)
    {
        var message = context.Message;

        var notification = TryBuildNotification(message);
        if (notification is null)
        {
            _logger.LogDebug(
                "Skipped in-app interview-rescheduled notification for interview {InterviewId}: " +
                "no candidate account behind the application",
                message.InterviewId);
            return;
        }

        var key = $"notifications:in-app:interview-rescheduled:{context.MessageId}";
        var written = await _idempotencyGuard.ProcessOnceAsync(key, async () =>
        {
            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync(context.CancellationToken);
        });

        if (!written)
        {
            _logger.LogInformation(
                "Skipped duplicate in-app interview-rescheduled notification for interview {InterviewId}",
                message.InterviewId);
            return;
        }

        _logger.LogInformation(
            "Wrote in-app interview-rescheduled notification for interview {InterviewId} " +
            "to candidate account {CandidateAccountId}",
            message.InterviewId,
            message.CandidateAccountId);
    }

    // Static so tests can check the mapping without a ConsumeContext.
    public static Notification? TryBuildNotification(InterviewRescheduledIntegrationEvent message)
    {
        if (message.CandidateAccountId is not { } candidateAccountId)
            return null;

        var payload = new NotificationPayloads.InterviewRescheduled(
            message.ApplicationId, message.JobTitle, message.InterviewType,
            message.PreviousScheduledAtUtc, message.ScheduledAtUtc,
            message.DurationMinutes, message.RoomToken);

        return Notification.ForCandidate(
            candidateAccountId,
            NotificationType.InterviewRescheduled,
            NotificationPayloads.Serialize(payload));
    }
}
