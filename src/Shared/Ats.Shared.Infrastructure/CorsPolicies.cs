namespace Ats.Shared.Infrastructure;

// CORS policy names. Unlike RateLimitPolicies (referenced by module controllers), the CORS policy is
// applied once in the host pipeline, so it lives next to CorsOptions rather than in the shared kernel.
public static class CorsPolicies
{
    // Allows the configured SPA origins to call the API; see the "Cors" configuration section.
    public const string Spa = "spa";
}
