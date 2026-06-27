namespace Ats.Shared.Infrastructure;

// Settings for the OpenAI-compatible chat API that backs CV parsing. Bound from the "Llm" section in
// Program.cs. Deliberately provider-neutral: GitHub Models, Groq, OpenRouter and others all speak the
// same /chat/completions shape, so switching providers is a config change (BaseUrl + Model + key),
// not a code change. Defaults point at GitHub Models, which is free with a GitHub token.
//
// Everything except the API key is non-secret tuning and lives in appsettings.json; the key (a GitHub
// PAT with Models access, or the provider's key) is read from User Secrets / environment variables and
// must never be committed.
public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    // Secret — supplied via User Secrets / env, not appsettings.json. Empty by default so a missing
    // key fails loudly at call time.
    public string ApiKey { get; init; } = "";

    // OpenAI-compatible base URL; "/chat/completions" is appended. GitHub Models by default.
    public string BaseUrl { get; init; } = "https://models.github.ai/inference";

    // Model id in the provider's format (GitHub Models uses "publisher/name").
    public string Model { get; init; } = "openai/gpt-4o-mini";

    public int MaxOutputTokens { get; init; } = 2048;

    // Per-attempt timeout enforced by Polly (the roadmap's 30s ceiling on the LLM call).
    public int TimeoutSeconds { get; init; } = 30;

    // Polly retry attempts for transient failures.
    public int RetryLimit { get; init; } = 3;
}
