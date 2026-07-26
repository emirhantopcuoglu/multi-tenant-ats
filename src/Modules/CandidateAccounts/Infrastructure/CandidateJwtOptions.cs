namespace Ats.Modules.CandidateAccounts.Infrastructure;

// Candidate tokens are signed with the SAME secret/issuer/audience as company tokens so the one JWT
// bearer scheme validates both. Rather than depend on the Tenants module's JwtOptions (which would
// couple the two modules), this binds the same "Jwt" configuration section into its own type. The
// shared secret lives in configuration — the correct place to share it — not in a cross-module code
// reference. The refresh lifetime is read from the same section as the company side's, so the two
// identities cannot drift to different session lengths by accident.
public sealed class CandidateJwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 7;
}
