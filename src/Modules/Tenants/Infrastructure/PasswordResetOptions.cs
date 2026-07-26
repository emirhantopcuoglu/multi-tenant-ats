namespace Ats.Modules.Tenants.Infrastructure;

// Where the mailed reset link points. This is a frontend URL, so it is configuration, not code — same
// pattern as InvitationOptions.AcceptBaseUrl. The path must stay one of SlugPolicy's reserved routes:
// it is a bare top-level SPA route, so an unreserved path could be claimed as a company slug and
// shadow the reset page.
public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    public string ResetBaseUrl { get; init; } = "http://localhost:5173/reset-password";

    // Identity's data-protection token provider defaults to a 24-hour lifespan. An hour is enough to
    // cover "open inbox, click link", and this token is a full account takeover for as long as it
    // lives, so the default is tightened to match the candidate side's PasswordResetRequest.
    public int ValidMinutes { get; init; } = 60;
}
