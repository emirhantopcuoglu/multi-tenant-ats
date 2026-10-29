using MassTransit;

namespace Ats.Modules.Applications.Infrastructure;

// Puts this consumer behind the EF outbox, so the stage move, the published message and the
// inbox row commit together. Without it, a broker failure after the save lost the announcement:
// the retry saw the stage already moved and published nothing. Only this consumer, not bus-wide.
public sealed class AdvanceToInterviewStageConsumerDefinition
    : ConsumerDefinition<AdvanceToInterviewStageConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<AdvanceToInterviewStageConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        // The bus-level retry wraps this filter, so each retry gets a fresh transaction.
        endpointConfigurator.UseEntityFrameworkOutbox<ApplicationsDbContext>(context);
    }
}
