namespace Ats.Shared.Infrastructure;

// How long personal data is kept once it is no longer needed.
public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    public int ClosedApplicationMonths { get; init; } = 12;
}
