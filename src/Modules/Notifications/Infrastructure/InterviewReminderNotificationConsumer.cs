using Ats.Modules.Notifications.Domain;
using Ats.Shared.Contracts.Notifications;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Notifications.Infrastructure;

// The in-app half of the interview reminder, so a candidate who has the product open sees the nudge
// without going to their inbox. Same shape as InterviewScheduledNotificationConsumer: skip when the
// application has no candidate account behind it, guard against duplicates.
//
// The guard is keyed on (interview, kind) rather than the message id, for the reason spelled out in
// InterviewReminderEmailConsumer: the producer is a sweep that may legitimately republish the same
// reminder under a new message id.
public sealed class InterviewReminderNotificationConsumer
    : IConsumer<InterviewReminderDueIntegrationEvent>
{
    private readonly NotificationsDbContext _db;
    private readonly IIdempotencyGuard _idempotencyGuard;
    private readonly ILogger<InterviewReminderNotificationConsumer> _logger;

    public InterviewReminderNotificationConsumer(
        NotificationsDbContext db,
        IIdempotencyGuard idempotencyGuard,
        ILogger<InterviewReminderNotificationConsumer> logger)
    {
        _db = db;
        _idempotencyGuard = idempotencyGuard;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<InterviewReminderDueIntegrationEvent> context)
    {
        var message = context.Message;

        var notification = TryBuildNotification(message);
        if (notification is null)
        {
            _logger.LogDebug(
                "Skipped in-app {ReminderKind} reminder for interview {InterviewId}: " +
                "no candidate account behind the application",
                message.Kind,
                message.InterviewId);
            return;
        }

        var key = $"notifications:in-app:interview-reminder:{message.InterviewId}:{message.Kind}";
        var written = await _idempotencyGuard.ProcessOnceAsync(key, async () =>
        {
            _db.Notifications.Add(notification);
            await _db.SaveChangesAsync(context.CancellationToken);
        });

        if (!written)
        {
            _logger.LogInformation(
                "Skipped duplicate in-app {ReminderKind} reminder for interview {InterviewId}",
                message.Kind,
                message.InterviewId);
            return;
        }

        _logger.LogInformation(
            "Wrote in-app {ReminderKind} reminder for interview {InterviewId} to candidate account {CandidateAccountId}",
            message.Kind,
            message.InterviewId,
            message.CandidateAccountId);
    }

    // Static for the same reason as its siblings: the payload mapping and the no-account skip are
    // unit-testable without standing up a ConsumeContext.
    public static Notification? TryBuildNotification(InterviewReminderDueIntegrationEvent message)
    {
        if (message.CandidateAccountId is not { } candidateAccountId)
            return null;

        var payload = new NotificationPayloads.InterviewReminder(
            message.ApplicationId, message.JobTitle, message.InterviewType,
            message.ScheduledAtUtc, message.DurationMinutes, message.RoomToken, message.Kind);

        return Notification.ForCandidate(
            candidateAccountId,
            NotificationType.InterviewReminder,
            NotificationPayloads.Serialize(payload));
    }
}
