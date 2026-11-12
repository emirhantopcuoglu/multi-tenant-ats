using System.Text;
using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Infrastructure;
using Ats.Shared.Contracts.Applications;
using Ats.Shared.Contracts.Jobs;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Ats.IntegrationTests.Applications;

// In-memory bus, no containers.
public sealed class CvParsingConcurrencyTests
{
    private const int QueuedCvs = 5;
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task no_more_cvs_than_the_limit_should_be_parsed_at_once()
    {
        var extractor = new BlockingPdfTextExtractor();
        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IFileStorage>(new RecordingFileStorage { DownloadContent = Encoding.ASCII.GetBytes("%PDF-1.4") })
            .AddSingleton<IPdfTextExtractor>(extractor)
            .AddSingleton<IDocxTextExtractor, DocxTextExtractor>()
            .AddSingleton<ICvParser, StubCvParser>()
            .AddSingleton<ICvParseResultRepository, NoOpCvParseResultRepository>()
            .AddSingleton<IJobDirectory>(new FakeJobDirectory(null))
            .AddMassTransitTestHarness(bus => bus.AddConsumer<CvParsingConsumer, CvParsingConsumerDefinition>())
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        for (var i = 0; i < QueuedCvs; i++)
            await harness.Bus.Publish(new CvParseRequestedIntegrationEvent(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "cv.pdf", Guid.NewGuid()));

        // Finish in finally: a failed assert would otherwise leave the parses blocked and the harness
        // waiting for them forever.
        try
        {
            await extractor.WaitForStartsAsync(CvParsingConsumerDefinition.MaxConcurrentParses, Wait);
            // Time for one more to start if nothing held it back.
            await Task.Delay(TimeSpan.FromMilliseconds(500));
            Assert.Equal(CvParsingConsumerDefinition.MaxConcurrentParses, extractor.MostAtOnce);
        }
        finally
        {
            extractor.Finish();
        }

        await extractor.WaitForStartsAsync(QueuedCvs - CvParsingConsumerDefinition.MaxConcurrentParses, Wait);
    }

    private sealed class BlockingPdfTextExtractor : IPdfTextExtractor
    {
        private readonly TaskCompletionSource _finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly SemaphoreSlim _started = new(0);
        private readonly Lock _lock = new();
        private int _running;

        public int MostAtOnce { get; private set; }

        public async Task<string> ExtractAsync(byte[] pdfBytes, CancellationToken cancellationToken = default)
        {
            lock (_lock)
                MostAtOnce = Math.Max(MostAtOnce, ++_running);
            _started.Release();

            await _finish.Task;
            lock (_lock)
                _running--;
            return "cv text";
        }

        public void Finish() => _finish.TrySetResult();

        public async Task WaitForStartsAsync(int count, TimeSpan timeout)
        {
            for (var i = 0; i < count; i++)
                Assert.True(await _started.WaitAsync(timeout));
        }
    }

    private sealed class StubCvParser : ICvParser
    {
        public Task<CvParseResult> ParseAsync(
            string cvText, string jobDescription, CancellationToken cancellationToken = default) =>
            Task.FromResult(new CvParseResult([], 0, [], [], CvJobFitRating.Weak, "", [], []));
    }

    private sealed class NoOpCvParseResultRepository : ICvParseResultRepository
    {
        public Task SaveAsync(
            Guid tenantId, Guid applicationId, CvParseResult result, DateTime parsedAtUtc,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<StoredCvParseResult?> GetByApplicationAsync(
            Guid applicationId, CancellationToken cancellationToken = default) =>
            Task.FromResult<StoredCvParseResult?>(null);

        public Task DeleteAsync(
            Guid tenantId, IReadOnlyCollection<Guid> applicationIds, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
