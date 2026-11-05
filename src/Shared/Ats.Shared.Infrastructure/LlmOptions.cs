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

    // Sized for the 8000 input tokens of the free GitHub Models tier: these two plus the system
    // prompt stay under it even for Turkish, which takes more tokens per character.
    public int MaxCvCharacters { get; init; } = 12_000;

    public int MaxJobDescriptionCharacters { get; init; } = 6_000;

    // Per attempt.
    public int TimeoutSeconds { get; init; } = 30;

    public int RetryLimit { get; init; } = 3;
}
