namespace Ats.Shared.Infrastructure;

// Settings for the Google Gemini API used by GeminiCvParser. Bound from the "Gemini" section in
// Program.cs. Gemini's AI Studio tier is free (no card), which is why it backs CV parsing here.
// Everything except the API key is non-secret tuning and lives in appsettings.json; the key is read
// from User Secrets / environment variables and must never be committed.
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    // Secret — supplied via User Secrets / env, not appsettings.json. Empty by default so a missing
    // key fails loudly at call time.
    public string ApiKey { get; init; } = "";

    // A free-tier model. Configurable so it can be swapped (e.g. to gemini-1.5-flash) without code.
    public string Model { get; init; } = "gemini-2.0-flash";

    public string BaseUrl { get; init; } = "https://generativelanguage.googleapis.com/v1beta";

    public int MaxOutputTokens { get; init; } = 2048;

    // Per-attempt timeout enforced by Polly (the roadmap's 30s ceiling on the LLM call).
    public int TimeoutSeconds { get; init; } = 30;

    // Polly retry attempts for transient failures.
    public int RetryLimit { get; init; } = 3;
}
