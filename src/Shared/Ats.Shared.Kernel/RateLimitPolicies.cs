namespace Ats.Shared.Kernel;

// Rate limiting policy names. Like Policies (authorization), these live in the shared kernel so any
// module's API can reference them in [EnableRateLimiting(...)] without depending on another module.
// Only per-IP is a named policy applied to specific endpoints; the per-tenant and per-user limits run
// as a global limiter (every authenticated request) and therefore need no name.
public static class RateLimitPolicies
{
    // Per-IP limit for unauthenticated abuse vectors: login, register, public application submit.
    public const string PerIp = "per-ip";
}
