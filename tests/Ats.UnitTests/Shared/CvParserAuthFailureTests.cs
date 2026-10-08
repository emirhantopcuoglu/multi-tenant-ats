using System.Net;
using Ats.Shared.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ats.UnitTests.Shared;

// A rejected API key used to travel as a plain HttpRequestException, so the resilience pipeline
// treated it as a blip: three retries, then the circuit breaker, then a MassTransit redelivery
// cycle — all guaranteed to fail identically, and the reason buried under the last of them. These
// tests pin the two halves of the fix: it is not retried, and it says what is wrong.
public class CvParserAuthFailureTests
{
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task A_rejected_key_should_fail_on_the_first_attempt(HttpStatusCode status)
    {
        // Arrange
        var handler = new CountingHandler(status);
        var parser = CreateParser(handler);

        // Act
        var exception = await Assert.ThrowsAsync<LlmAuthenticationException>(
            () => parser.ParseAsync("cv text", "job description"));

        // Assert — one call, not RetryLimit + 1
        Assert.Equal(1, handler.Calls);
        Assert.Contains("Llm:ApiKey", exception.Message);
        Assert.Contains(((int)status).ToString(), exception.Message);
    }

    [Fact]
    public async Task A_server_error_should_still_be_retried()
    {
        // The counterpart: without it, silencing the retry for everything would pass the test above.
        var handler = new CountingHandler(HttpStatusCode.InternalServerError);
        var parser = CreateParser(handler);

        await Assert.ThrowsAnyAsync<Exception>(() => parser.ParseAsync("cv text", "job description"));

        // RetryLimit attempts on top of the first one.
        Assert.Equal(RetryLimit + 1, handler.Calls);
    }

    private const int RetryLimit = 2;

    private static OpenAiCompatibleCvParser CreateParser(HttpMessageHandler handler) =>
        new(
            new StubHttpClientFactory(handler),
            Options.Create(new LlmOptions { ApiKey = "test-key", RetryLimit = RetryLimit }),
            NullLogger<OpenAiCompatibleCvParser>.Instance);

    // Counts attempts so "was it retried" is observable; the pipeline's own state is not.
    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;

        public CountingHandler(HttpStatusCode status) => _status = status;

        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(_status));
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

        // disposeHandler: false — the parser disposes the client it is handed, and the test still
        // needs to read the call count off the handler afterwards.
        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }
}
