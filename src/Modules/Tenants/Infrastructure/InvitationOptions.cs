namespace Ats.Modules.Tenants.Infrastructure;

public sealed class InvitationOptions
{
    public const string SectionName = "Invitation";

    public int ValidDays { get; init; } = 7;

    // The path must stay in SlugPolicy's reserved list (see InvitationLinkTests).
    public string AcceptBaseUrl { get; init; } = "http://localhost:5173/accept-invitation";
}
