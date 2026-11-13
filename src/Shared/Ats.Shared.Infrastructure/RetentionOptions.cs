namespace Ats.Shared.Infrastructure;

// How long personal data is kept once it is no longer needed.
public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    public int ClosedApplicationMonths { get; init; } = 12;

    public int AuditLogMonths { get; init; } = 24;

    public int ReadNotificationDays { get; init; } = 90;

    // Counted from the expiry. Revoked sessions are kept until then, because presenting one ends its family.
    public int ExpiredTokenDays { get; init; } = 30;
}
