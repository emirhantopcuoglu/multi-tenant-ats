namespace Ats.Shared.Infrastructure;

public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    // Only needs to outlive redeliveries and retries, which happen well within a day.
    public int RetentionHours { get; init; } = 24;
}
