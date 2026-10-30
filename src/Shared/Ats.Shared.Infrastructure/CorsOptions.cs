namespace Ats.Shared.Infrastructure;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    // Explicit origins only: the policy allows credentials, which can't be combined with a wildcard.
    // Empty by default, so a missing setting allows nothing.
    public string[] AllowedOrigins { get; init; } = [];
}
