using Ats.Shared.Contracts.Notifications;
using MassTransit;
using MediatR;

namespace Ats.Modules.Interviews.Application.Events;

// Bridges the reminder onto the message bus. The mapping is the same shape as its siblings — domain
// enums become strings so the contract carries none of this module's types.
//
// What differs is the failure policy, and deliberately so: this one does NOT go through
// IntegrationEventBridge. That helper swallows broker failures because its callers run inside a
// recruiter's request, where the change is already committed and failing the call would only invite
// a retry of something that already took effect. Here there is no request and no committed change to
// protect — delivering the reminder IS the whole job. So a broker failure propagates, the sweep
// aborts before clearing anything, and Hangfire retries the run. The consumers deduplicate on
// (interview, kind), so the reminders that did go out are not sent twice.
public sealed class PublishInterviewReminderDueIntegrationEvent
    : INotificationHandler<InterviewReminderDueEvent>
{
    private readonly IBus _bus;

    public PublishInterviewReminderDueIntegrationEvent(IBus bus) => _bus = bus;

    public Task Handle(InterviewReminderDueEvent notification, CancellationToken cancellationToken) =>
        _bus.Publish(
            new InterviewReminderDueIntegrationEvent(
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
                notification.Kind.ToString(),
                notification.TenantId),
            cancellationToken);
}
