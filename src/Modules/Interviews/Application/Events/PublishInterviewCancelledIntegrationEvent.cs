using Ats.Shared.Contracts.Notifications;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Interviews.Application.Events;

// Bridges "this interview was called off" onto the bus. The cancellation reason crosses as a string
// for the same reason the interview type does — a contract must not carry this module's enums. The
// recruiter's free-text note is not on the domain event at all, so it cannot be mapped here by
// mistake. See IntegrationEventBridge for transport and failure policy.
public sealed class PublishInterviewCancelledIntegrationEvent
    : INotificationHandler<InterviewCancelledEvent>
{
    private readonly IBus _bus;
    private readonly ILogger<PublishInterviewCancelledIntegrationEvent> _logger;

    public PublishInterviewCancelledIntegrationEvent(
        IBus bus, ILogger<PublishInterviewCancelledIntegrationEvent> logger)
    {
        _bus = bus;
        _logger = logger;
    }

    public Task Handle(InterviewCancelledEvent notification, CancellationToken cancellationToken) =>
        IntegrationEventBridge.PublishOrLogAsync(
            _bus,
            new InterviewCancelledIntegrationEvent(
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
                notification.Reason.ToString(),
                notification.TenantId),
            _logger,
            notification.InterviewId,
            notification.ApplicationId,
            cancellationToken);
}
