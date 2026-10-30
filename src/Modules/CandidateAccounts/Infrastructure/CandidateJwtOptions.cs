namespace Ats.Modules.CandidateAccounts.Infrastructure;

// Binds the same "Jwt" section as the Tenants module, so one bearer scheme validates both token
// types without the modules referencing each other.
public sealed class CandidateJwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 7;
}
