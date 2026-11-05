using Ats.Shared.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ats.UnitTests.Shared;

public class CvParserInputLimitTests
{
    private const int CvLimit = 100;
    private const int JobDescriptionLimit = 50;

    // 'x', 'q' and 'z' appear nowhere else in the message, so counting them shows what was sent.
    [Fact]
    public async Task a_cv_over_the_limit_should_be_cut_to_the_limit_before_it_is_sent()
    {
        var handler = new RecordingLlmHandler();
        var cv = new string('x', CvLimit) + new string('z', 1_000_000);

        await CreateParser(handler).ParseAsync(cv, "job description");

        Assert.Equal(CvLimit, handler.LastUserMessage.Count(c => c == 'x'));
        Assert.DoesNotContain('z', handler.LastUserMessage);
    }

    [Fact]
    public async Task a_job_description_over_the_limit_should_be_cut_to_the_limit_before_it_is_sent()
    {
        var handler = new RecordingLlmHandler();
        var jobDescription = new string('q', JobDescriptionLimit) + new string('z', 1_000_000);

        await CreateParser(handler).ParseAsync("cv text", jobDescription);

        Assert.Equal(JobDescriptionLimit, handler.LastUserMessage.Count(c => c == 'q'));
        Assert.DoesNotContain('z', handler.LastUserMessage);
    }

    private static OpenAiCompatibleCvParser CreateParser(HttpMessageHandler handler) =>
        new(
            new StubHttpClientFactory(handler),
            Options.Create(new LlmOptions
            {
                ApiKey = "test-key",
                MaxCvCharacters = CvLimit,
                MaxJobDescriptionCharacters = JobDescriptionLimit
            }),
            NullLogger<OpenAiCompatibleCvParser>.Instance);
}
