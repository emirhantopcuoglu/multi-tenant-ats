using MassTransit;

namespace Ats.Modules.Applications.Infrastructure;

// A parse can start a PDF worker that uses up to its own memory limit, so the number of parses at
// once is what bounds the total.
public sealed class CvParsingConsumerDefinition : ConsumerDefinition<CvParsingConsumer>
{
    public const int MaxConcurrentParses = 2;

    public CvParsingConsumerDefinition() => ConcurrentMessageLimit = MaxConcurrentParses;
}
