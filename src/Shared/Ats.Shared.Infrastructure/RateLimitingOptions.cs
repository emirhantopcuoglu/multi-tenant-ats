namespace Ats.Shared.Infrastructure;

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    // Anonymous endpoints like login and register, per client IP.
    public int PerIpPermitLimit { get; init; } = 5;

    public int PerUserPermitLimit { get; init; } = 60;

    public int PerTenantPermitLimit { get; init; } = 100;

    // Shared by all three limits.
    public int WindowSeconds { get; init; } = 60;
}
