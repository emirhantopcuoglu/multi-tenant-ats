namespace Ats.Modules.Tenants.Infrastructure;

// The path must stay in SlugPolicy's reserved list, or a company slug could shadow it.
public sealed class EmailConfirmationOptions
{
    public const string SectionName = "EmailConfirmation";

    public string ConfirmBaseUrl { get; init; } = "http://localhost:5173/confirm-email";
}
