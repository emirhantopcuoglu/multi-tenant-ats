using Ats.Shared.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ats.UnitTests.Shared;

public class CvParserResponseShapeTests
{
    // gpt-oss-120b on Groq answered the extraction prompt this way.
    [Fact]
    public async Task a_date_the_model_writes_as_a_number_should_be_read_as_text()
    {
        var handler = new FakeLlmHandler();
        handler.Replies.Enqueue(
            """{"recent_positions":[{"title":"Visual Designer","start_date":2021,"end_date":"present"}]}""");

        var result = await CreateParser(handler).ParseAsync("cv text", "job description");

        var position = Assert.Single(result.RecentPositions);
        Assert.Equal("2021", position.StartDate);
        Assert.Equal("present", position.EndDate);
    }

    // Without it the model guesses how long an ongoing role has run: the same CV came back with 0 and
    // with 5 years of experience.
    [Fact]
    public async Task both_calls_should_be_told_todays_date()
    {
        var handler = new FakeLlmHandler();
        var before = DateTime.UtcNow.ToString("yyyy-MM-dd");

        await CreateParser(handler).ParseAsync("cv text", "job description");

        var after = DateTime.UtcNow.ToString("yyyy-MM-dd");
        Assert.All(handler.UserMessages, message =>
            Assert.True(message.Contains(before) || message.Contains(after), message));
    }

    private static OpenAiCompatibleCvParser CreateParser(HttpMessageHandler handler) =>
        new(
            new StubHttpClientFactory(handler),
            Options.Create(new LlmOptions { ApiKey = "test-key" }),
            NullLogger<OpenAiCompatibleCvParser>.Instance);
}
