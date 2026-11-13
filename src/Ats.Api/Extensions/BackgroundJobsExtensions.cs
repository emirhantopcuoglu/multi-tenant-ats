using Ats.Modules.Applications.Infrastructure;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Modules.Interviews.Infrastructure;
using Ats.Modules.Jobs.Infrastructure;
using Ats.Modules.Notifications.Infrastructure;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
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
        builder.Services.Configure<RetentionOptions>(
            builder.Configuration.GetSection(RetentionOptions.SectionName));

        builder.Services.AddHangfire(hangfire => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(postgres =>
                postgres.UseNpgsqlConnection(builder.Configuration.GetConnectionString("Postgres"))));
        builder.Services.AddHangfireServer();

        builder.Services.AddScoped<ExpiredInvitationCleanupJob>();
        builder.Services.AddScoped<InterviewReminderJob>();

        // Every module that stores files registers its owner here, or the cleanup deletes its files.
        builder.Services.AddScoped<IStoredFileOwner, ApplicationCvFileOwner>();
        builder.Services.AddScoped<IStoredFileOwner, CandidateAccountCvFileOwner>();
        builder.Services.AddScoped<OrphanFileCleanupJob>();

        builder.Services.AddScoped<ApplicationPersonalDataEraser>();

        // Every module that keeps a candidate's data registers its eraser here, or the data outlives the account.
        builder.Services.AddScoped<ICandidateDataEraser, ApplicationCandidateDataEraser>();
        builder.Services.AddScoped<ICandidateDataEraser, InterviewCandidateDataEraser>();
        builder.Services.AddScoped<ICandidateDataEraser, NotificationCandidateDataEraser>();
        builder.Services.AddScoped<CandidateAccountDeletionJob>();

        // Every module that keeps data about an application registers here too, or it outlives the
        // retention period. Applications erases its own rows last, inside the job.
        builder.Services.AddScoped<IApplicationDataEraser, InterviewCandidateDataEraser>();
        builder.Services.AddScoped<IApplicationDataEraser, NotificationCandidateDataEraser>();
        builder.Services.AddScoped<ClosedApplicationRetentionJob>();

        // Every module that keeps records past their use registers a cleaner here, and so does every schema
        // with an AuditLogs table, or those records are kept for good.
        builder.Services.AddScoped<IRetentionCleaner, AuditLogRetentionCleaner<TenantsDbContext>>();
        builder.Services.AddScoped<IRetentionCleaner, AuditLogRetentionCleaner<JobsDbContext>>();
        builder.Services.AddScoped<IRetentionCleaner, AuditLogRetentionCleaner<ApplicationsDbContext>>();
        builder.Services.AddScoped<IRetentionCleaner, AuditLogRetentionCleaner<InterviewsDbContext>>();
        builder.Services.AddScoped<IRetentionCleaner, TenantsRetentionCleaner>();
        builder.Services.AddScoped<IRetentionCleaner, CandidateAccountsRetentionCleaner>();
        builder.Services.AddScoped<IRetentionCleaner, NotificationRetentionCleaner>();
        builder.Services.AddScoped<RetentionCleanupJob>();

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

        RecurringJob.AddOrUpdate<OrphanFileCleanupJob>(
            "orphan-file-cleanup",
            job => job.CleanupAsync(CancellationToken.None),
            hangfireOptions.OrphanFileCleanupCron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        RecurringJob.AddOrUpdate<CandidateAccountDeletionJob>(
            "candidate-account-deletion",
            job => job.EraseDueAccountsAsync(CancellationToken.None),
            hangfireOptions.CandidateAccountDeletionCron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        RecurringJob.AddOrUpdate<ClosedApplicationRetentionJob>(
            "closed-application-retention",
            job => job.EraseDueApplicationsAsync(CancellationToken.None),
            hangfireOptions.ClosedApplicationRetentionCron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        RecurringJob.AddOrUpdate<RetentionCleanupJob>(
            "retention-cleanup",
            job => job.CleanupAsync(CancellationToken.None),
            hangfireOptions.RetentionCleanupCron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        return app;
    }
}
