namespace Ats.Shared.Infrastructure;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    // Anonymous endpoints like login and register, per client IP.
    public int PerIpPermitLimit { get; init; } = 5;

    // Public job and company pages, per client IP.
    public int PublicReadPermitLimit { get; init; } = 60;

    public int PerUserPermitLimit { get; init; } = 60;

    public int PerTenantPermitLimit { get; init; } = 100;

    // Shared by all the limits.
    public int WindowSeconds { get; init; } = 60;
}
