using System.Globalization;
using System.Net;
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

// CV parser for any OpenAI-compatible /chat/completions API (GitHub Models by default). Asks for
// json_object output with the fields spelled out in the prompt. Polly handles retry, circuit
// breaker and timeout. Stateless, so registered as a singleton.
public sealed class OpenAiCompatibleCvParser : ICvParser
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ResiliencePipeline _pipeline;
    private readonly LlmOptions _options;
    private readonly ILogger<OpenAiCompatibleCvParser> _logger;

    // json_object mode guarantees JSON but not a schema, so the fields are listed in the prompt (which
    // must contain the word "JSON"). The prompt also forbids reasoning about anything related to
    // protected characteristics; the model has no other guardrail.
    private const string SystemPrompt =
        "You extract structured data from a candidate's CV and assess their fit for a specific job. " +
        "Use only information present in the CV and the job description. Do not invent or infer " +
        "values that are not stated. If a field is unknown, use an empty string, 0, or an empty " +
        "array. Return a JSON object with exactly these fields: skills (array of strings), " +
        "total_experience_years (number), education (array of objects with degree, institution, " +
        "year), recent_positions (array of objects with title, company, start_date, end_date), " +
        "job_fit_rating (exactly one of \"Strong\", \"Moderate\", \"Weak\"), fit_summary (2-3 " +
        "sentences grounded in specifics from the CV, explaining the rating), matched_requirements " +
        "(array of concrete skills/technologies the job description asks for and the CV shows), " +
        "missing_requirements (array of concrete skills/technologies the job description asks for " +
        "that the CV does not show). Base job_fit_rating, matched_requirements, and " +
        "missing_requirements strictly on concrete technical skills, tools, and experience the job " +
        "description names. Never mention or infer employment gaps, job-hopping, age, how long ago " +
        "someone graduated, or any other characteristic unrelated to the job's stated technical " +
        "requirements -- these must never appear in fit_summary, matched_requirements, or " +
        "missing_requirements. " +
        // Same language as the job description; the CV if the job is gone.
        "Write fit_summary in the same language as the job description; if no job description is " +
        "given, use the language of the CV. Everything else is extracted data, not prose: leave " +
        "skills, education, recent_positions, matched_requirements and missing_requirements in the " +
        "language they appear in, and never translate the name of a technology, tool or " +
        "qualification. job_fit_rating stays one of the three English values named above.";

    // No naming policy: member names are sent as the OpenAI keys.
    private static readonly JsonSerializerOptions SerializeOptions = new(JsonSerializerDefaults.General)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // The model sometimes returns numbers as strings, or "" for a missing year. JsonNumberHandling
    // still throws on "", so the two exposed fields have lenient converters.
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    // Number or numeric string; anything else becomes 0.
    private sealed class LenientInt32Converter : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
                return reader.GetInt32();
            if (reader.TokenType == JsonTokenType.String && int.TryParse(reader.GetString(), out var value))
                return value;
            return 0;
        }

        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(value);
    }

    private sealed class LenientDoubleConverter : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number)
                return reader.GetDouble();
            if (reader.TokenType == JsonTokenType.String &&
                double.TryParse(reader.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                return value;
            return 0;
        }

        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(value);
    }

    public OpenAiCompatibleCvParser(
        IHttpClientFactory httpClientFactory,
        IOptions<LlmOptions> options,
        ILogger<OpenAiCompatibleCvParser> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;

        // Retry wraps the circuit breaker, which wraps the per-attempt timeout.
        _pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = _options.RetryLimit,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(1),
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(IsWorthRetrying)
            })
            .AddCircuitBreaker(new Polly.CircuitBreaker.CircuitBreakerStrategyOptions
            {
                FailureRatio = 0.5,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(15),
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(IsWorthRetrying)
            })
            .AddTimeout(TimeSpan.FromSeconds(_options.TimeoutSeconds))
            .Build();
    }

    // A rejected key or a cancellation won't succeed on retry.
    private static bool IsWorthRetrying(Exception exception) =>
        exception is not OperationCanceledException and not LlmAuthenticationException;

    public async Task<CvParseResult> ParseAsync(
        string cvText, string jobDescription, CancellationToken cancellationToken = default)
    {
        var userContent =
            "Job description:\n\n" + jobDescription +
            "\n\nCandidate CV:\n\n" + cvText;

        var requestBody = new
        {
            model = _options.Model,
            temperature = 0,
            max_tokens = _options.MaxOutputTokens,
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = userContent }
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

            // Fails on the first attempt: a rejected key is not transient.
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new LlmAuthenticationException(
                    $"The LLM provider rejected the API key with {(int)response.StatusCode} " +
                    $"{response.StatusCode}. Check the Llm:ApiKey secret — a GitHub token for the " +
                    "default provider expires and has to be reissued with Models access.");
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: token);
        }, cancellationToken);

        if (chatResponse?.Usage is { } usage)
        {
            _logger.LogInformation(
                "CV parsed via {Model}. Prompt tokens: {PromptTokens}, completion tokens: {CompletionTokens}",
                _options.Model, usage.PromptTokens, usage.CompletionTokens);
        }

        var json = chatResponse?.Choices?.FirstOrDefault()?.Message?.Content;
        if (string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("The model returned no content for the CV parse request.");

        CvParseDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<CvParseDto>(json, DeserializeOptions);
        }
        catch (JsonException ex)
        {
            // Log the raw JSON on a shape mismatch, or the cause is lost once this throws.
            _logger.LogError(ex, "CV parse response had an unexpected shape: {Json}", json);
            throw;
        }

        if (dto is null)
            throw new InvalidOperationException("The model returned a CV parse result that could not be deserialized.");

        return dto.ToResult();
    }

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

    private sealed record CvParseDto(
        [property: JsonPropertyName("skills")] List<string>? Skills,
        [property: JsonPropertyName("total_experience_years"), JsonConverter(typeof(LenientDoubleConverter))]
        double TotalExperienceYears,
        [property: JsonPropertyName("education")] List<EducationDto>? Education,
        [property: JsonPropertyName("recent_positions")] List<PositionDto>? RecentPositions,
        [property: JsonPropertyName("job_fit_rating")] string? JobFitRating,
        [property: JsonPropertyName("fit_summary")] string? FitSummary,
        [property: JsonPropertyName("matched_requirements")] List<string>? MatchedRequirements,
        [property: JsonPropertyName("missing_requirements")] List<string>? MissingRequirements)
    {
        public CvParseResult ToResult() => new(
            Skills ?? [],
            TotalExperienceYears,
            Education?.Select(e => new CvEducation(e.Degree ?? "", e.Institution ?? "", e.Year)).ToList() ?? [],
            RecentPositions?
                .Select(p => new CvPosition(p.Title ?? "", p.Company ?? "", p.StartDate ?? "", p.EndDate ?? ""))
                .ToList() ?? [],
            ParseFitRating(JobFitRating),
            FitSummary ?? "",
            MatchedRequirements ?? [],
            MissingRequirements ?? []);
    }

    // Unparseable ratings fall back to Moderate, never to Strong or Weak.
    public static CvJobFitRating ParseFitRating(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "strong" => CvJobFitRating.Strong,
        "weak" => CvJobFitRating.Weak,
        _ => CvJobFitRating.Moderate
    };

    private sealed record EducationDto(
        [property: JsonPropertyName("degree")] string? Degree,
        [property: JsonPropertyName("institution")] string? Institution,
        [property: JsonPropertyName("year"), JsonConverter(typeof(LenientInt32Converter))]
        int Year);

    private sealed record PositionDto(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("company")] string? Company,
        [property: JsonPropertyName("start_date")] string? StartDate,
        [property: JsonPropertyName("end_date")] string? EndDate);
}
