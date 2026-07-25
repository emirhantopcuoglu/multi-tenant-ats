using Ats.Shared.Contracts.Notifications;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Interviews.Application.Events;

// Bridges the in-process domain event onto the message bus. The domain enum becomes a string here:
// the contract must not carry this module's types. Transport and failure policy live in
// IntegrationEventBridge — see its comment for why this publishes through IBus and why a broker
// failure is swallowed.
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

    public Task Handle(InterviewScheduledEvent notification, CancellationToken cancellationToken) =>
        IntegrationEventBridge.PublishOrLogAsync(
            _bus,
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
            _logger,
            notification.InterviewId,
            notification.ApplicationId,
            cancellationToken);
}
