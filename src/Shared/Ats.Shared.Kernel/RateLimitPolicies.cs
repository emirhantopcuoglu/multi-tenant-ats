namespace Ats.Shared.Kernel;

// Only per-IP needs a name. Per-tenant and per-user run as the global limiter.
public static class RateLimitPolicies
{
    public const string PerIp = "per-ip";
}
