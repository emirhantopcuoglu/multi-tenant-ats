using System.Net;
using Ats.Shared.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ats.UnitTests.Shared;

// The parser makes one attempt; whether there is another is up to the message retry.
public class CvParserFailureTests
{
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task A_rejected_key_should_fail_on_the_first_attempt(HttpStatusCode status)
    {
        // Arrange
        var handler = new FakeLlmHandler { Status = status };
        var parser = CreateParser(handler);

        // Act
        var exception = await Assert.ThrowsAsync<LlmAuthenticationException>(
            () => parser.ParseAsync("cv text", "job description"));

        // Assert: one call
        Assert.Equal(1, handler.Calls);
        Assert.Contains("Llm:ApiKey", exception.Message);
        Assert.Contains(((int)status).ToString(), exception.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task a_request_the_provider_refuses_should_fail_as_rejected_on_the_first_attempt(HttpStatusCode status)
    {
        var handler = new FakeLlmHandler { Status = status };

        var exception = await Assert.ThrowsAsync<LlmRequestRejectedException>(
            () => CreateParser(handler).ParseAsync("cv text", "job description"));

        Assert.Equal(1, handler.Calls);
        Assert.Contains(((int)status).ToString(), exception.Message);
    }

    // 408 and 429 mean "try again later", so they must stay retryable, unlike the rest of 4xx.
    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task a_transient_failure_should_be_left_to_the_message_retry(HttpStatusCode status)
    {
        var handler = new FakeLlmHandler { Status = status };

        var exception = await Record.ExceptionAsync(
            () => CreateParser(handler).ParseAsync("cv text", "job description"));

        Assert.IsType<HttpRequestException>(exception);
        Assert.Equal(1, handler.Calls);
    }

    // The breaker is shared, so if refusals of one CV counted, every other CV would wait out the break.
    [Fact]
    public async Task refused_requests_should_not_open_the_circuit_for_other_cvs()
    {
        var handler = new FakeLlmHandler { Status = HttpStatusCode.RequestEntityTooLarge };
        var parser = CreateParser(handler);
        for (var i = 0; i < 10; i++)
            await Record.ExceptionAsync(() => parser.ParseAsync("oversized cv", "job description"));

        handler.Status = HttpStatusCode.OK;
        var result = await parser.ParseAsync("another candidate's cv", "job description");

        Assert.NotNull(result);
    }

    private static OpenAiCompatibleCvParser CreateParser(HttpMessageHandler handler) =>
        new(
            new StubHttpClientFactory(handler),
            Options.Create(new LlmOptions { ApiKey = "test-key" }),
            NullLogger<OpenAiCompatibleCvParser>.Instance);
}
