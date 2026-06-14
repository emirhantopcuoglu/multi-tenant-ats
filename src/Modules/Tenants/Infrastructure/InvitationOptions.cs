namespace Ats.Modules.Tenants.Infrastructure;

public sealed class InvitationOptions
{
    public const string SectionName = "Invitation";

    public int ValidDays { get; init; } = 7;
    public string AcceptBaseUrl { get; init; } = "http://localhost:5000/accept-invite";
}
