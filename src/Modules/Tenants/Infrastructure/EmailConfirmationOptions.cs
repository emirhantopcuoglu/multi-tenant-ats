namespace Ats.Modules.Tenants.Infrastructure;

// Where the mailed confirmation link points — the SPA's confirm page, not the API. Configuration
// rather than code, the same pattern as InvitationOptions.AcceptBaseUrl and PasswordResetOptions.
//
// The path must stay one of SlugPolicy's reserved routes: it is a bare top-level SPA route, so a
// tenant that registered this word as its slug would shadow it. See InvitationLinkTests for the check
// that keeps this honest — that test exists because the invitation link once pointed at the wrong host
// and path entirely, and nothing noticed until a real invite went out dead.
public sealed class EmailConfirmationOptions
{
    public const string SectionName = "EmailConfirmation";

    public string ConfirmBaseUrl { get; init; } = "http://localhost:5173/confirm-email";
}
