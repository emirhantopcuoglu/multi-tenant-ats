using MassTransit;

namespace Ats.Modules.Applications.Infrastructure;

// Puts AdvanceToInterviewStageConsumer's endpoint behind the transactional outbox, which is what
// makes its stage move and its announcement one atomic unit.
//
// Without it the consumer committed the move with SaveChangesAsync and then published straight to
// RabbitMQ. A broker hiccup between the two lost the announcement permanently: MassTransit retried
// the whole message, but the retry re-read an application that was already in the Interview stage,
// so InterviewStageAdvancement.FindTarget returned null and the consumer returned before reaching
// the publish. The stage had changed and the candidate was never told.
//
// With the outbox, MassTransit opens one transaction around the consume: the InboxState row, the
// consumer's own SaveChangesAsync and the published message all commit together or not at all.
// ConsumeContext.Publish then writes an OutboxMessage row instead of talking to the broker, so it
// cannot fail on a broker outage, and the delivery service forwards it afterwards with its own
// retries. A retry that does reach the consumer again is dropped by the inbox rather than
// re-running against half-applied state.
//
// Scoped to this one consumer through a definition rather than applied bus-wide: every other
// consumer keeps its current behaviour, and the queue name stays whatever the kebab-case formatter
// already produced, so no in-flight message is stranded on an old queue.
public sealed class AdvanceToInterviewStageConsumerDefinition
    : ConsumerDefinition<AdvanceToInterviewStageConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<AdvanceToInterviewStageConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        // The bus-level UseMessageRetry is configured before ConfigureEndpoints, so it wraps this
        // filter: a retry starts a fresh transaction instead of reusing a rolled-back one.
        endpointConfigurator.UseEntityFrameworkOutbox<ApplicationsDbContext>(context);
    }
}
