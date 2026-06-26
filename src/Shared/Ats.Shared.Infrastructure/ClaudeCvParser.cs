using System.Text.Json;
using System.Text.Json.Serialization;
using Anthropic;
using Anthropic.Models.Messages;
using Ats.Shared.Kernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Ats.Shared.Infrastructure;

// Claude-backed ICvParser. Sends the CV text to the Anthropic Messages API and asks for the result
// as JSON constrained by a schema (structured outputs), so the model returns valid, parseable JSON
// instead of prose we would have to scrape.
//
// Resilience is owned here, by Polly, not by the SDK: the client's own retries are disabled
// (MaxRetries = 0) and a single pipeline applies retry -> circuit breaker -> per-attempt timeout.
// Concentrating it in one place is the roadmap's requirement (retry + circuit breaker + 30s timeout)
// and avoids the double-retry that leaving the SDK retries on would cause.
//
// Stateless apart from the reusable client and pipeline, so it is registered as a singleton.
public sealed class ClaudeCvParser : ICvParser
{
    private readonly AnthropicClient _client;
    private readonly ResiliencePipeline _pipeline;
    private readonly AnthropicOptions _options;
    private readonly ILogger<ClaudeCvParser> _logger;

    // The JSON Schema we constrain the model's output to. Built once: it never changes per request.
    // No nullable unions — unknown values come back as their empty form (see CvParseResult) which
    // keeps the schema within the structured-output feature's supported subset.
    private static readonly IReadOnlyDictionary<string, JsonElement> ResponseSchema = BuildSchema();

    private const string SystemPrompt =
        "You extract structured data from a candidate's CV. Use only information present in the CV. " +
        "Do not invent or infer values that are not stated. If a field is unknown, use an empty " +
        "string, 0, or an empty array as appropriate. Return only the requested fields.";

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public ClaudeCvParser(IOptions<AnthropicOptions> options, ILogger<ClaudeCvParser> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Disable the SDK's built-in retries so Polly is the single owner of resilience.
        _client = new AnthropicClient { ApiKey = _options.ApiKey, MaxRetries = 0 };

        // Outer-to-inner: retry wraps the circuit breaker wraps a per-attempt timeout. A transient
        // failure is retried; repeated failures trip the breaker and fail fast (protecting the API
        // and our worker threads); each individual attempt is bounded by the timeout.
        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = _options.RetryLimit,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
                // Don't retry a cancellation (caller gave up) — only genuine faults.
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not OperationCanceledException)
            })
            .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(15),
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(ex => ex is not OperationCanceledException)
            })
            .AddTimeout(TimeSpan.FromSeconds(_options.TimeoutSeconds))
            .Build();
    }

    public async Task<CvParseResult> ParseAsync(string cvText, CancellationToken cancellationToken = default)
    {
        var parameters = new MessageCreateParams
        {
            Model = _options.Model,
            MaxTokens = _options.MaxTokens,
            System = SystemPrompt,
            Messages =
            [
                new MessageParam
                {
                    Role = Role.User,
                    Content =
                        "Extract skills, total years of experience, education, and recent positions " +
                        "from the following CV:\n\n" + cvText
                }
            ],
            OutputConfig = new OutputConfig
            {
                Format = new JsonOutputFormat
                {
                    Schema = new Dictionary<string, JsonElement>(ResponseSchema)
                }
            }
        };

        var response = await _pipeline.ExecuteAsync(
            async ct => await _client.Messages.Create(parameters, cancellationToken: ct),
            cancellationToken);

        // Cost visibility: the roadmap asks the token count to be logged on every call.
        _logger.LogInformation(
            "CV parsed via Claude. Input tokens: {InputTokens}, output tokens: {OutputTokens}",
            response.Usage.InputTokens, response.Usage.OutputTokens);

        var json = response.Content
            .Select(block => block.Value)
            .OfType<TextBlock>()
            .Select(block => block.Text)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("Claude returned no text content for the CV parse request.");

        var dto = JsonSerializer.Deserialize<CvParseDto>(json, DeserializeOptions)
            ?? throw new InvalidOperationException("Claude returned a CV parse result that could not be deserialized.");

        return dto.ToResult();
    }

    private static IReadOnlyDictionary<string, JsonElement> BuildSchema()
    {
        var properties = new
        {
            skills = new { type = "array", items = new { type = "string" } },
            total_experience_years = new { type = "number" },
            education = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        degree = new { type = "string" },
                        institution = new { type = "string" },
                        year = new { type = "integer" }
                    },
                    required = new[] { "degree", "institution", "year" },
                    additionalProperties = false
                }
            },
            recent_positions = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        title = new { type = "string" },
                        company = new { type = "string" },
                        start_date = new { type = "string" },
                        end_date = new { type = "string" }
                    },
                    required = new[] { "title", "company", "start_date", "end_date" },
                    additionalProperties = false
                }
            }
        };

        return new Dictionary<string, JsonElement>
        {
            ["type"] = JsonSerializer.SerializeToElement("object"),
            ["properties"] = JsonSerializer.SerializeToElement(properties),
            ["required"] = JsonSerializer.SerializeToElement(
                new[] { "skills", "total_experience_years", "education", "recent_positions" }),
            ["additionalProperties"] = JsonSerializer.SerializeToElement(false)
        };
    }

    // Deserialization shape matching the schema's snake_case fields, mapped to the Kernel's
    // CvParseResult. Kept private to the implementation so the port stays free of JSON concerns.
    private sealed record CvParseDto(
        [property: JsonPropertyName("skills")] List<string>? Skills,
        [property: JsonPropertyName("total_experience_years")] double TotalExperienceYears,
        [property: JsonPropertyName("education")] List<EducationDto>? Education,
        [property: JsonPropertyName("recent_positions")] List<PositionDto>? RecentPositions)
    {
        public CvParseResult ToResult() => new(
            Skills ?? [],
            TotalExperienceYears,
            Education?.Select(e => new CvEducation(e.Degree ?? "", e.Institution ?? "", e.Year)).ToList() ?? [],
            RecentPositions?
                .Select(p => new CvPosition(p.Title ?? "", p.Company ?? "", p.StartDate ?? "", p.EndDate ?? ""))
                .ToList() ?? []);
    }

    private sealed record EducationDto(
        [property: JsonPropertyName("degree")] string? Degree,
        [property: JsonPropertyName("institution")] string? Institution,
        [property: JsonPropertyName("year")] int Year);

    private sealed record PositionDto(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("company")] string? Company,
        [property: JsonPropertyName("start_date")] string? StartDate,
        [property: JsonPropertyName("end_date")] string? EndDate);
}
