namespace Ats.Shared.Infrastructure;

// Cross-origin settings for the SPA, bound from the "Cors" configuration section in Program.cs.
// The frontend (Ats.Web) is served from a different origin than the API, so the browser blocks its
// requests unless the API opts in via CORS. Keeping the allowed origins in configuration (rather than
// hard-coded in Program.cs) lets each environment list its own front-end origin without a code change.
public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    // Exact front-end origins allowed to call the API (scheme + host + port, no trailing slash).
    // Must be an explicit list, never a wildcard: the spa policy sends credentials, and the CORS spec
    // forbids combining AllowAnyOrigin with AllowCredentials. Empty by default so a misconfigured
    // environment fails closed (no origin allowed) rather than open.
    public string[] AllowedOrigins { get; init; } = [];
}
