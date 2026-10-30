namespace Ats.Modules.Tenants.Infrastructure;

// The path must stay in SlugPolicy's reserved list, or a company slug could shadow it.
public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    public string ResetBaseUrl { get; init; } = "http://localhost:5173/reset-password";

    // Identity defaults to 24 hours; a reset token is too powerful for that.
    public int ValidMinutes { get; init; } = 60;
}
