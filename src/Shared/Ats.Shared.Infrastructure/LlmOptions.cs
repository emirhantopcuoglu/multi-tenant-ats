namespace Ats.Shared.Infrastructure;

// Any OpenAI-compatible chat API (GitHub Models, Groq, OpenRouter). Switching is a config change.
// Only ApiKey is secret.
public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    // From User Secrets or environment variables. Empty by default so a missing key fails loudly.
    public string ApiKey { get; init; } = "";

    // "/chat/completions" is appended.
    public string BaseUrl { get; init; } = "https://models.github.ai/inference";

    public string Model { get; init; } = "openai/gpt-4o-mini";

    public int MaxOutputTokens { get; init; } = 2048;

    // Per attempt.
    public int TimeoutSeconds { get; init; } = 30;

    public int RetryLimit { get; init; } = 3;
}
