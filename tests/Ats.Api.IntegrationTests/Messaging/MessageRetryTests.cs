using Ats.Api.Extensions;
using Ats.Shared.Infrastructure;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Ats.Api.IntegrationTests.Messaging;

// The bus retry is the only retry a CV parse gets, so this pins what it retries. In-memory bus, no containers.
public sealed class MessageRetryTests
{
    private static readonly RabbitMqOptions Options = new()
    {
        RetryLimit = 2,
        RetryInitialIntervalSeconds = 0,
        RetryMaxIntervalSeconds = 0
    };

    [Fact]
    public async Task a_request_the_llm_provider_refused_should_not_be_retried()
    {
        Assert.Equal(1, await CountAttemptsAsync(new LlmRequestRejectedException("refused")));
    }

    [Fact]
    public async Task a_rejected_llm_key_should_not_be_retried()
    {
        Assert.Equal(1, await CountAttemptsAsync(new LlmAuthenticationException("rejected")));
    }

    [Fact]
    public async Task a_transient_failure_should_be_retried_up_to_the_limit()
    {
        Assert.Equal(Options.RetryLimit + 1, await CountAttemptsAsync(new HttpRequestException("unavailable")));
    }

    private static async Task<int> CountAttemptsAsync(Exception failure)
    {
        var attempts = new Attempts(failure);
        await using var provider = new ServiceCollection()
            .AddSingleton(attempts)
            .AddMassTransitTestHarness(bus =>
            {
                bus.AddConsumer<FailingConsumer>();
                bus.UsingInMemory((context, configurator) =>
                {
                    configurator.UseMessageRetry(retry => MessagingExtensions.ConfigureMessageRetry(retry, Options));
                    configurator.ConfigureEndpoints(context);
                });
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        await harness.Bus.Publish(new Probe());

        // The fault is published once the retry gives up, so the count is final by then.
        Assert.True(await harness.Published.Any<Fault<Probe>>());
        return attempts.Count;
    }

    public sealed record Probe;

    private sealed class Attempts(Exception failure)
    {
        public Exception Failure { get; } = failure;

        public int Count { get; set; }
    }

    private sealed class FailingConsumer(Attempts attempts) : IConsumer<Probe>
    {
        public Task Consume(ConsumeContext<Probe> context)
        {
            attempts.Count++;
            throw attempts.Failure;
        }
    }
}
