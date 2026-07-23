using Ats.Shared.Contracts.Notifications;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Interviews.Application.Events;

// Bridges the in-process domain event onto the message bus. Unlike the Applications module's
// bridges, this one publishes through IBus — straight to the broker — NOT through the scoped
// IPublishEndpoint. MassTransit 8.x supports exactly one bus outbox per container, and it lives in
// the Applications DbContext: a scoped publish from here would be captured by that outbox and
// dropped, because an interview request never saves the Applications context. The domain enum
// becomes a string here: the contract must not carry this module's types.
//
// The trade-off of the direct publish: no outbox means no atomicity with the interview row. The
// command handler therefore raises the event only AFTER its SaveChanges succeeds (a notification
// about an uncommitted interview would be a lie), and a broker failure here is logged and
// swallowed rather than propagated — the interview is already committed, so failing the request
// would only push the recruiter into scheduling a duplicate. The notification is best-effort by
// design until the stack can afford a second outbox.
public sealed class PublishInterviewScheduledIntegrationEvent
    : INotificationHandler<InterviewScheduledEvent>
{
    private readonly IBus _bus;
    private readonly ILogger<PublishInterviewScheduledIntegrationEvent> _logger;

    public PublishInterviewScheduledIntegrationEvent(
        IBus bus, ILogger<PublishInterviewScheduledIntegrationEvent> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    public async Task Handle(InterviewScheduledEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            await _bus.Publish(
                new InterviewScheduledIntegrationEvent(
                    notification.InterviewId,
                    notification.ApplicationId,
                    notification.JobId,
                    notification.JobTitle,
                    notification.CandidateId,
                    notification.CandidateAccountId,
                    notification.CandidateEmail,
                    notification.CandidateFirstName,
                    notification.Type.ToString(),
                    notification.ScheduledAtUtc,
                    notification.DurationMinutes,
                    notification.RoomToken,
                    notification.TenantId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish InterviewScheduledIntegrationEvent for interview {InterviewId} " +
                "(application {ApplicationId}); the interview is committed but no notification will go out",
                notification.InterviewId,
                notification.ApplicationId);
        }
    }
}
