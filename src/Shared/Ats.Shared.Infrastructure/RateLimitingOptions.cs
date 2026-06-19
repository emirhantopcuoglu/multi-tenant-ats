namespace Ats.Shared.Infrastructure;

// Rate limiting thresholds, bound from the "RateLimiting" configuration section in Program.cs.
// Keeping the limits in configuration (rather than as magic numbers in Program.cs) lets each
// environment tune them without a code change. Defaults match the Sprint 4.4 roadmap.
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    // Unauthenticated endpoints (login/register/public apply), partitioned by client IP.
    public int PerIpPermitLimit { get; init; } = 5;

    // Authenticated traffic, partitioned by the user id (sub) claim.
    public int PerUserPermitLimit { get; init; } = 60;

    // Authenticated traffic, partitioned by the tenant_id claim — a fair share across a whole company.
    public int PerTenantPermitLimit { get; init; } = 100;

    // Fixed window length shared by all three limits. The roadmap states every limit "per minute".
    public int WindowSeconds { get; init; } = 60;
}
