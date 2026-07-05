using System.Globalization;
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
    // The fit fields exist so a recruiter gets a job-specific read in seconds instead of re-reading
    // the whole CV themselves (the actual reason this feature is worth having) -- so the prompt is
    // explicit both about what to compare against (the job description that follows the CV in the
    // user message) and, just as importantly, what NOT to reason about: anything adjacent to a
    // protected characteristic. That instruction is enforced here, at the only place that can
    // enforce it -- the model itself has no other guardrail.
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
        "missing_requirements.";

    // No naming policy: anonymous-object member names (model, max_tokens, response_format, ...) are
    // sent verbatim as the OpenAI wire keys.
    private static readonly JsonSerializerOptions SerializeOptions = new(JsonSerializerDefaults.General)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // json_object mode guarantees valid JSON syntax but not per-field types or values: the model has
    // returned a quoted number ("total_experience_years": "5") and, for a CV with no stated graduation
    // year, an empty string ("year": ""). Neither is a bare number nor a parseable numeric string, so
    // the two numeric fields most exposed to this (TotalExperienceYears, EducationDto.Year) carry their
    // own lenient converters below instead of relying on JsonNumberHandling, which still throws on a
    // non-numeric string like "".
    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    // Reads a number, a numeric string, or falls back to 0 for anything else (empty string, "N/A",
    // null) -- the CV parse prompt asks for 0 on unknown values, but the model doesn't always comply.
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

        CvParseDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<CvParseDto>(json, DeserializeOptions);
        }
        catch (JsonException ex)
        {
            // The model's JSON is syntactically valid (json_object mode guarantees that) but its
            // field *values* are not contractually guaranteed, so log the raw payload on a shape
            // mismatch -- without it, a new quirk is undiagnosable after the fact (the text is
            // gone once this throws).
            _logger.LogError(ex, "CV parse response had an unexpected shape: {Json}", json);
            throw;
        }

        if (dto is null)
            throw new InvalidOperationException("The model returned a CV parse result that could not be deserialized.");

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

    // Moderate is the safe fallback when the model doesn't return exactly one of the three asked-for
    // values -- never silently defaulting to the most flattering (Strong) or least flattering (Weak)
    // reading of a rating we couldn't actually parse.
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
