using Ats.Modules.Interviews.Infrastructure;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Infrastructure;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;

namespace Ats.Api.Extensions;

public static class BackgroundJobsExtensions
{
    // Hangfire background jobs (Sprint 5.4). Jobs are stored in PostgreSQL (Hangfire's own "hangfire"
    // schema, created automatically and kept separate from our EF migrations) so they survive restarts,
    // and run on a server hosted in this process. In a multi-instance deployment Hangfire's storage-level
    // locks ensure a recurring job runs on only one instance at a time — the reason to use it over a raw
    // BackgroundService.
    public static IHostApplicationBuilder AddBackgroundJobs(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<HangfireOptions>(
            builder.Configuration.GetSection(HangfireOptions.SectionName));

        builder.Services.AddHangfire(hangfire => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(postgres =>
                postgres.UseNpgsqlConnection(builder.Configuration.GetConnectionString("Postgres"))));
        builder.Services.AddHangfireServer();

        // Resolved per execution inside the Hangfire job scope; scheduled via MapRecurringJobs below,
        // once the app is built.
        builder.Services.AddScoped<ExpiredInvitationCleanupJob>();
        builder.Services.AddScoped<InterviewReminderJob>();

        return builder;
    }

    // Hangfire dashboard at /hangfire. LocalRequestsOnlyAuthorizationFilter restricts it to localhost:
    // the dashboard exposes job data and trigger/delete controls, and the API's auth is bearer-token
    // based (no cookies), so a browser cannot carry a JWT here. Real authentication for a remote
    // dashboard is a production hardening task (Sprint 8); in dev, local-only is the correct guard.
    public static WebApplication UseHangfireDashboardEndpoint(this WebApplication app)
    {
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = [new LocalRequestsOnlyAuthorizationFilter()]
        });

        return app;
    }

    // Register/refresh the recurring jobs. AddOrUpdate keys on the job id, so a restart updates the
    // existing schedule rather than creating duplicates.
    public static WebApplication MapRecurringJobs(this WebApplication app)
    {
        var hangfireOptions = app.Configuration.GetSection(HangfireOptions.SectionName).Get<HangfireOptions>()
            ?? new HangfireOptions();

        RecurringJob.AddOrUpdate<ExpiredInvitationCleanupJob>(
            "expired-invitation-cleanup",
            job => job.CleanupAsync(CancellationToken.None),
            hangfireOptions.ExpiredInvitationCleanupCron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        // The interview reminder sweep. Runs far more often than the cleanup above because its
        // lateness is visible to a candidate: the cadence is the worst case by which a "starting soon"
        // nudge can arrive behind schedule, so it has to stay well inside the room's 10-minute lead.
        RecurringJob.AddOrUpdate<InterviewReminderJob>(
            "interview-reminders",
            job => job.SendDueRemindersAsync(CancellationToken.None),
            hangfireOptions.InterviewReminderCron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        return app;
    }
}
