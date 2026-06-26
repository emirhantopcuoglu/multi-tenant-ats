using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ats.Shared.Kernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;

namespace Ats.Shared.Infrastructure;

// Gemini-backed ICvParser. Sends the CV text to Google's Generative Language API and asks for the
// result as JSON constrained by a response schema (Gemini's structured output), so the model returns
// valid, parseable JSON instead of prose. Gemini's AI Studio tier is free, which is why it backs CV
// parsing rather than a paid provider — the ICvParser port makes the choice an Infrastructure detail.
//
// Resilience is owned by Polly: a single pipeline applies retry -> circuit breaker -> per-attempt
// timeout (the roadmap's requirement). The HTTP client comes from IHttpClientFactory so handler
// reuse and pooling are handled correctly.
//
// Stateless apart from the reusable pipeline, so it is registered as a singleton.
public sealed class GeminiCvParser : ICvParser
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ResiliencePipeline _pipeline;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiCvParser> _logger;

    // The response schema Gemini constrains the model's JSON to. Built once. Gemini's schema dialect
    // uses upper-case type names and does not support additionalProperties (unlike JSON Schema), so
    // it is kept separate from the prose CvParseResult. Unknown values come back as their empty form.
    private static readonly object ResponseSchema = BuildSchema();

    // No naming policy: anonymous-object member names are sent verbatim, so the schema's snake_case
    // field names and Gemini's camelCase request keys are both preserved exactly.
    private static readonly JsonSerializerOptions SerializeOptions = new(JsonSerializerDefaults.General)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private const string SystemPrompt =
        "You extract structured data from a candidate's CV. Use only information present in the CV. " +
        "Do not invent or infer values that are not stated. If a field is unknown, use an empty " +
        "string, 0, or an empty array as appropriate. Return only the requested fields.";

    public GeminiCvParser(
        IHttpClientFactory httpClientFactory,
        IOptions<GeminiOptions> options,
        ILogger<GeminiCvParser> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;

        // Outer-to-inner: retry wraps the circuit breaker wraps a per-attempt timeout. A transient
        // failure is retried; repeated failures trip the breaker and fail fast; each attempt is
        // bounded by the timeout.
        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = _options.RetryLimit,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
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
        var requestBody = new
        {
            systemInstruction = new { parts = new[] { new { text = SystemPrompt } } },
            contents = new[]
            {
                new
                {
                    parts = new[]
                    {
                        new
                        {
                            text = "Extract skills, total years of experience, education, and recent " +
                                   "positions from the following CV:\n\n" + cvText
                        }
                    }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                responseSchema = ResponseSchema,
                temperature = 0,
                maxOutputTokens = _options.MaxOutputTokens
            }
        };

        // The API key travels as a query parameter, so this URL must never be logged.
        var url = $"{_options.BaseUrl}/models/{_options.Model}:generateContent?key={_options.ApiKey}";

        using var client = _httpClientFactory.CreateClient();

        var geminiResponse = await _pipeline.ExecuteAsync(async token =>
        {
            using var response = await client.PostAsJsonAsync(url, requestBody, SerializeOptions, token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<GeminiResponse>(cancellationToken: token);
        }, cancellationToken);

        // Cost visibility: the roadmap asks the token count to be logged on every call.
        if (geminiResponse?.UsageMetadata is { } usage)
        {
            _logger.LogInformation(
                "CV parsed via Gemini. Prompt tokens: {PromptTokens}, output tokens: {OutputTokens}",
                usage.PromptTokenCount, usage.CandidatesTokenCount);
        }

        var json = geminiResponse?.Candidates?
            .FirstOrDefault()?.Content?.Parts?
            .FirstOrDefault()?.Text;

        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("Gemini returned no text content for the CV parse request.");

        var dto = JsonSerializer.Deserialize<CvParseDto>(json, DeserializeOptions)
            ?? throw new InvalidOperationException("Gemini returned a CV parse result that could not be deserialized.");

        return dto.ToResult();
    }

    private static object BuildSchema() => new
    {
        type = "OBJECT",
        properties = new
        {
            skills = new { type = "ARRAY", items = new { type = "STRING" } },
            total_experience_years = new { type = "NUMBER" },
            education = new
            {
                type = "ARRAY",
                items = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        degree = new { type = "STRING" },
                        institution = new { type = "STRING" },
                        year = new { type = "INTEGER" }
                    },
                    required = new[] { "degree", "institution", "year" }
                }
            },
            recent_positions = new
            {
                type = "ARRAY",
                items = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        title = new { type = "STRING" },
                        company = new { type = "STRING" },
                        start_date = new { type = "STRING" },
                        end_date = new { type = "STRING" }
                    },
                    required = new[] { "title", "company", "start_date", "end_date" }
                }
            }
        },
        required = new[] { "skills", "total_experience_years", "education", "recent_positions" }
    };

    // ---- Gemini response envelope (only the fields we read) ----
    private sealed record GeminiResponse(
        [property: JsonPropertyName("candidates")] List<GeminiCandidate>? Candidates,
        [property: JsonPropertyName("usageMetadata")] GeminiUsage? UsageMetadata);

    private sealed record GeminiCandidate(
        [property: JsonPropertyName("content")] GeminiContent? Content);

    private sealed record GeminiContent(
        [property: JsonPropertyName("parts")] List<GeminiPart>? Parts);

    private sealed record GeminiPart(
        [property: JsonPropertyName("text")] string? Text);

    private sealed record GeminiUsage(
        [property: JsonPropertyName("promptTokenCount")] int PromptTokenCount,
        [property: JsonPropertyName("candidatesTokenCount")] int CandidatesTokenCount);

    // ---- Parsed CV payload (snake_case fields), mapped to the Kernel's CvParseResult ----
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
