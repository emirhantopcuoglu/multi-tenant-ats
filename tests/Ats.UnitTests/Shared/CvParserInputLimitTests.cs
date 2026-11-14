using Ats.Shared.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Ats.UnitTests.Shared;

public class CvParserInputLimitTests
{
    private const int CvLimit = 100;
    private const int JobDescriptionLimit = 50;

    // '~', '^' and '|' appear nowhere else in what is sent, so counting them across every call shows
    // what went out.
    [Fact]
    public async Task a_cv_over_the_limit_should_be_cut_to_the_limit_before_it_is_sent()
    {
        var handler = new FakeLlmHandler();
        var cv = new string('~', CvLimit) + new string('|', 1_000_000);

        await CreateParser(handler).ParseAsync(cv, "job description");

        var sent = string.Concat(handler.UserMessages);
        Assert.Equal(CvLimit, sent.Count(c => c == '~'));
        Assert.DoesNotContain('|', sent);
    }

    [Fact]
    public async Task a_job_description_over_the_limit_should_be_cut_to_the_limit_before_it_is_sent()
    {
        var handler = new FakeLlmHandler();
        var jobDescription = new string('^', JobDescriptionLimit) + new string('|', 1_000_000);

        await CreateParser(handler).ParseAsync("cv text", jobDescription);

        var sent = string.Concat(handler.UserMessages);
        Assert.Equal(JobDescriptionLimit, sent.Count(c => c == '^'));
        Assert.DoesNotContain('|', sent);
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
