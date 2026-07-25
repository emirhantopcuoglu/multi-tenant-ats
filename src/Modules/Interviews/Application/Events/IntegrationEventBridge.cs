using MassTransit;
using Microsoft.Extensions.Logging;

namespace Ats.Modules.Interviews.Application.Events;

// The failure policy shared by every interview→bus bridge in this module. The mapping differs per
// event, so each bridge stays its own class; what does not differ is what happens when the broker is
// unreachable, and that had no business being copied three times.
//
// Publishing goes through IBus (straight to the broker), NOT the scoped IPublishEndpoint:
// MassTransit 8.x allows exactly one bus outbox per container and it lives in the Applications
// DbContext, so a scoped publish from here would be captured by that outbox and silently dropped —
// an interview request never saves the Applications context.
//
// The trade-off of a direct publish is no atomicity with the interview row. Callers therefore raise
// their domain event only AFTER SaveChanges succeeds (announcing an uncommitted change would be a
// lie), and a broker failure is logged and swallowed rather than propagated: the change is already
// committed, so failing the request would only push the recruiter into retrying an action that
// already took effect. Notifications are best-effort by design until the stack can afford a second
// outbox.
internal static class IntegrationEventBridge
{
    public static async Task PublishOrLogAsync<TIntegrationEvent>(
        IBus bus,
        TIntegrationEvent integrationEvent,
        ILogger logger,
        Guid interviewId,
        Guid applicationId,
        CancellationToken cancellationToken)
        where TIntegrationEvent : class
    {
        try
        {
            await bus.Publish(integrationEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to publish {EventType} for interview {InterviewId} (application {ApplicationId}); " +
                "the change is committed but no notification will go out",
                typeof(TIntegrationEvent).Name,
                interviewId,
                applicationId);
        }
    }
}
