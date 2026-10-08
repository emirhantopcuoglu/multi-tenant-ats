namespace Ats.Shared.Kernel;

/* Brute-force protection for both login forms, bound from the "LoginLockout" configuration section.
   Shared because the company and candidate sides are the same problem and must not drift into two
   different answers — the company side hands these to Identity's own lockout, the candidate side to
   the counters on CandidateAccount.

   Why this exists at all: the per-IP rate limiter counts *requests from an address*, so it cannot
   see a distributed attack. A thousand addresses each spending their five attempts per minute on
   one account is, to that limiter, a thousand well-behaved clients. Lockout counts *failures
   against an account*, which is the axis the attack actually runs along. It is also the layer that
   survives Redis being down — FailOpenRateLimiter deliberately lets traffic through when it is, so
   without this, an outage removes the only guard on the login form.

   Deliberately temporary. A lockout that never expires hands anyone who knows an email address a
   permanent denial of service against that person, which is a worse bug than the one being fixed —
   so the window closes on its own, and a password reset clears it early. */
public sealed class LoginLockoutOptions
{
    public const string SectionName = "LoginLockout";

    /* Consecutive failures before the account stops answering. Low enough to stop guessing, high
       enough to survive someone cycling through the passwords they actually use. */
    public int MaxFailedAttempts { get; init; } = 5;

    /* How long the account stays shut. Long enough that guessing becomes pointless — five attempts
       per fifteen minutes is twenty an hour — and short enough that a locked-out user who walks away
       for a coffee comes back to a working login. */
    public int LockoutMinutes { get; init; } = 15;

    public TimeSpan LockoutDuration => TimeSpan.FromMinutes(LockoutMinutes);
}
