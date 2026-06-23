namespace Ats.Shared.Infrastructure;

// Settings for the message idempotency guard (Sprint 5.5). Mirrors the other options classes
// (RedisOptions/RabbitMqOptions/HangfireOptions): bound from the "Idempotency" configuration section
// in Program.cs so the retention window is environment-tunable rather than a magic number in code.
public sealed class IdempotencyOptions
{
    public const string SectionName = "Idempotency";

    // How long a processed-message marker is kept in Redis. It only needs to outlive the window in
    // which a duplicate could still arrive — redeliveries, retries, and the occasional manual error-queue
    // replay all happen well within a day, so 24h is a safe default with a bounded memory cost.
    public int RetentionHours { get; init; } = 24;
}
