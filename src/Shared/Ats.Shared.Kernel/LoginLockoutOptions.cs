namespace Ats.Shared.Kernel;

// Account lockout for both login forms. Unlike the per-IP rate limiter it counts failures per
// account, so it catches attacks spread over many addresses and still works when Redis is down.
// Always temporary; a password reset clears it early.
public sealed class LoginLockoutOptions
{
    public const string SectionName = "LoginLockout";

    public int MaxFailedAttempts { get; init; } = 5;

    public int LockoutMinutes { get; init; } = 15;

    public TimeSpan LockoutDuration => TimeSpan.FromMinutes(LockoutMinutes);
}
