namespace Ats.Shared.Infrastructure;

// Settings for the Anthropic Claude API used by ClaudeCvParser. Bound from the "Anthropic" section
// in Program.cs. Everything here except the API key is non-secret tuning and lives in
// appsettings.json; the key is read from User Secrets / environment variables and must never be
// committed — the same split as the MinIO and RabbitMQ credentials.
public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    // Secret — supplied via User Secrets / env, not appsettings.json. Empty by default so a missing
    // key fails loudly at call time rather than silently using someone else's.
    public string ApiKey { get; init; } = "";

    public string Model { get; init; } = "claude-opus-4-8";

    // Output cap for the parse response. The structured CV JSON is small, so this is generous.
    public int MaxTokens { get; init; } = 2048;

    // Per-attempt timeout enforced by Polly. The roadmap calls for a 30s ceiling on the LLM call.
    public int TimeoutSeconds { get; init; } = 30;

    // Polly retry attempts for transient failures (the SDK's own retries are disabled so resilience
    // is owned in one place).
    public int RetryLimit { get; init; } = 3;
}
