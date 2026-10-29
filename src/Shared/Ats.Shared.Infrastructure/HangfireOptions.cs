namespace Ats.Shared.Infrastructure;

// Hangfire uses the main Postgres connection and keeps its own "hangfire" schema.
public sealed class HangfireOptions
{
    public const string SectionName = "Hangfire";

    public string ExpiredInvitationCleanupCron { get; init; } = "0 0 * * *";

    // The interval is the worst-case delay. Every five minutes keeps the starting-soon reminder
    // (due ten minutes before) from arriving after the interview starts.
    public string InterviewReminderCron { get; init; } = "*/5 * * * *";
}
