namespace Ats.Modules.Tenants.Infrastructure;

public sealed class InvitationOptions
{
    public const string SectionName = "Invitation";

    public int ValidDays { get; init; } = 7;

    // Where the mailed invitation link points: the SPA's accept page, not the API. The path must stay
    // one of SlugPolicy's reserved routes — see InvitationLinkTests.
    public string AcceptBaseUrl { get; init; } = "http://localhost:5173/accept-invitation";
}
