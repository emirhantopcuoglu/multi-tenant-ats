using Ats.Modules.Interviews.Infrastructure;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Infrastructure;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;

namespace Ats.Api.Extensions;

public static class BackgroundJobsExtensions
{
    // Hangfire stores jobs in Postgres (its own schema) and locks recurring jobs so only one instance
    // runs them.
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

        builder.Services.AddScoped<ExpiredInvitationCleanupJob>();
        builder.Services.AddScoped<InterviewReminderJob>();

        return builder;
    }

    // Localhost only: a browser can't send the JWT here. Remote access needs proper auth first.
    public static WebApplication UseHangfireDashboardEndpoint(this WebApplication app)
    {
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = [new LocalRequestsOnlyAuthorizationFilter()]
        });

        return app;
    }

    // AddOrUpdate keys on the job id, so restarts don't create duplicates.
    public static WebApplication MapRecurringJobs(this WebApplication app)
    {
        var hangfireOptions = app.Configuration.GetSection(HangfireOptions.SectionName).Get<HangfireOptions>()
            ?? new HangfireOptions();

        RecurringJob.AddOrUpdate<ExpiredInvitationCleanupJob>(
            "expired-invitation-cleanup",
            job => job.CleanupAsync(CancellationToken.None),
            hangfireOptions.ExpiredInvitationCleanupCron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        // Runs often: the interval is the worst-case delay for the starting-soon reminder.
        RecurringJob.AddOrUpdate<InterviewReminderJob>(
            "interview-reminders",
            job => job.SendDueRemindersAsync(CancellationToken.None),
            hangfireOptions.InterviewReminderCron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        return app;
    }
}
