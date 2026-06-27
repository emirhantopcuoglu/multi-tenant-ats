using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ats.Shared.Kernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;

namespace Ats.Shared.Infrastructure;

// CV parser backed by any OpenAI-compatible /chat/completions API (GitHub Models by default; Groq,
// OpenRouter, etc. by config). It asks for JSON output (response_format = json_object) and spells out
// the exact fields in the system prompt, so the model returns parseable JSON in the shape we map.
// GitHub Models is free with a GitHub token, which is why it backs CV parsing rather than a paid
// provider — the ICvParser port keeps the choice an Infrastructure detail.
//
// Resilience is owned by Polly: retry -> circuit breaker -> per-attempt timeout (the roadmap's
// requirement). The HTTP client comes from IHttpClientFactory. Stateless apart from the reusable
// pipeline, so it is registered as a singleton.
public sealed class OpenAiCompatibleCvParser : ICvParser
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ResiliencePipeline _pipeline;
    private readonly LlmOptions _options;
    private readonly ILogger<OpenAiCompatibleCvParser> _logger;

    // json_object mode guarantees valid JSON but not a schema, so the field contract is stated in the
    // prompt. Unknown values come back as their empty form (see CvParseResult). The word "JSON" must
    // appear in the prompt for json_object mode to be accepted.
    private const string SystemPrompt =
        "You extract structured data from a candidate's CV. Use only information present in the CV. " +
        "Do not invent or infer values that are not stated. If a field is unknown, use an empty " +
        "string, 0, or an empty array. Return a JSON object with exactly these fields: skills (array " +
        "of strings), total_experience_years (number), education (array of objects with degree, " +
        "institution, year), recent_positions (array of objects with title, company, start_date, " +
        "end_date).";

    // No naming policy: anonymous-object member names (model, max_tokens, response_format, ...) are
    // sent verbatim as the OpenAI wire keys.
    private static readonly JsonSerializerOptions SerializeOptions = new(JsonSerializerDefaults.General)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public OpenAiCompatibleCvParser(
        IHttpClientFactory httpClientFactory,
        IOptions<LlmOptions> options,
        ILogger<OpenAiCompatibleCvParser> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;

        // Outer-to-inner: retry wraps the circuit breaker wraps a per-attempt timeout.
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
            model = _options.Model,
            temperature = 0,
            max_tokens = _options.MaxOutputTokens,
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = "Extract the fields from this CV:\n\n" + cvText }
            }
        };

        var url = $"{_options.BaseUrl.TrimEnd('/')}/chat/completions";

        using var client = _httpClientFactory.CreateClient();

        var chatResponse = await _pipeline.ExecuteAsync(async token =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(requestBody, options: SerializeOptions)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            using var response = await client.SendAsync(request, token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: token);
        }, cancellationToken);

        // Cost visibility: the roadmap asks the token count to be logged on every call.
        if (chatResponse?.Usage is { } usage)
        {
            _logger.LogInformation(
                "CV parsed via {Model}. Prompt tokens: {PromptTokens}, completion tokens: {CompletionTokens}",
                _options.Model, usage.PromptTokens, usage.CompletionTokens);
        }

        var json = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("The model returned no content for the CV parse request.");

        var dto = JsonSerializer.Deserialize<CvParseDto>(json, DeserializeOptions)
            ?? throw new InvalidOperationException("The model returned a CV parse result that could not be deserialized.");

        return dto.ToResult();
    }

    // ---- OpenAI chat completions response envelope (only the fields we read) ----
    private sealed record ChatResponse(
        [property: JsonPropertyName("choices")] List<ChatChoice>? Choices,
        [property: JsonPropertyName("usage")] ChatUsage? Usage);

    private sealed record ChatChoice(
        [property: JsonPropertyName("message")] ChatMessage? Message);

    private sealed record ChatMessage(
        [property: JsonPropertyName("content")] string? Content);

    private sealed record ChatUsage(
        [property: JsonPropertyName("prompt_tokens")] int PromptTokens,
        [property: JsonPropertyName("completion_tokens")] int CompletionTokens);

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
