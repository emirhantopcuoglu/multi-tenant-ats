namespace Ats.Shared.Infrastructure;

// Settings for Hangfire background jobs, introduced in Sprint 5.4. Hangfire stores its jobs in
// PostgreSQL (its own "hangfire" schema, separate from our EF migrations) and runs them on a server
// hosted in the API. Mirrors MongoOptions/RedisOptions/RabbitMqOptions: bound from the "Hangfire"
// configuration section in Program.cs. The storage reuses the existing Postgres connection string, so
// the only thing to configure here is the cleanup schedule.
public sealed class HangfireOptions
{
    public const string SectionName = "Hangfire";

    // Cron expression for the expired-invitation cleanup job. Defaults to daily at midnight UTC.
    // Kept in config (not a hardcoded Cron.Daily()) so operators can tune the cadence per environment
    // without a code change.
    public string ExpiredInvitationCleanupCron { get; init; } = "0 0 * * *";
}
