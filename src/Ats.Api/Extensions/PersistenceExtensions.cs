using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Application.Applications;
using Ats.Modules.Applications.Infrastructure;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Modules.Interviews.Application;
using Ats.Modules.Interviews.Infrastructure;
using Ats.Modules.Jobs.Application;
using Ats.Modules.Jobs.Infrastructure;
using Ats.Modules.Notifications.Application;
using Ats.Modules.Notifications.Infrastructure;
using Ats.Modules.Tenants.Application;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Ats.Api.Extensions;

public static class PersistenceExtensions
{
    // One method: a new module always adds a DbContext and its Application DI together.
    public static IHostApplicationBuilder AddPersistence(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUser, CurrentUser>();

        builder.Services.AddScoped<TenantContext>();
        builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        builder.Services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<TenantContext>());
        builder.Services.AddScoped<TenantSaveChangesInterceptor>();
        builder.Services.AddScoped<AuditableSaveChangesInterceptor>();
        builder.Services.AddScoped<AuditLogInterceptor>();

        builder.Services.AddDbContext<TenantsDbContext>((sp, options) =>
            options
                .UseNpgsql(
                    builder.Configuration.GetConnectionString("Postgres"),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tenants"))
                .AddInterceptors(
                    sp.GetRequiredService<TenantSaveChangesInterceptor>(),
                    sp.GetRequiredService<AuditableSaveChangesInterceptor>(),
                    sp.GetRequiredService<AuditLogInterceptor>()));

        builder.Services.AddDbContext<JobsDbContext>((sp, options) =>
            options
                .UseNpgsql(
                    builder.Configuration.GetConnectionString("Postgres"),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "jobs"))
                .AddInterceptors(
                    sp.GetRequiredService<TenantSaveChangesInterceptor>(),
                    sp.GetRequiredService<AuditableSaveChangesInterceptor>(),
                    sp.GetRequiredService<AuditLogInterceptor>()));

        builder.Services.AddScoped<IJobsDbContext>(sp => sp.GetRequiredService<JobsDbContext>());
        builder.Services.AddJobsApplication();

        builder.Services.AddDbContext<ApplicationsDbContext>((sp, options) =>
            options
                .UseNpgsql(
                    builder.Configuration.GetConnectionString("Postgres"),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "applications"))
                .AddInterceptors(
                    sp.GetRequiredService<TenantSaveChangesInterceptor>(),
                    sp.GetRequiredService<AuditableSaveChangesInterceptor>(),
                    sp.GetRequiredService<AuditLogInterceptor>()));

        builder.Services.AddScoped<IApplicationsDbContext>(sp => sp.GetRequiredService<ApplicationsDbContext>());
        builder.Services.AddApplicationsApplication();

        builder.Services.AddDbContext<InterviewsDbContext>((sp, options) =>
            options
                .UseNpgsql(
                    builder.Configuration.GetConnectionString("Postgres"),
                    npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "interviews"))
                .AddInterceptors(
                    sp.GetRequiredService<TenantSaveChangesInterceptor>(),
                    sp.GetRequiredService<AuditableSaveChangesInterceptor>(),
                    sp.GetRequiredService<AuditLogInterceptor>()));

        builder.Services.AddScoped<IInterviewsDbContext>(sp => sp.GetRequiredService<InterviewsDbContext>());
        builder.Services.AddInterviewsApplication();

        // No tenant or audit interceptors: nothing here is ITenantScoped or IAuditable.
        builder.Services.AddDbContext<CandidateAccountsDbContext>(options =>
            options.UseNpgsql(
                builder.Configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "candidate_accounts")));

        // No tenant or audit interceptors: notifications are scoped to their recipient.
        builder.Services.AddDbContext<NotificationsDbContext>(options =>
            options.UseNpgsql(
                builder.Configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "notifications")));

        builder.Services.AddScoped<INotificationsDbContext>(sp => sp.GetRequiredService<NotificationsDbContext>());
        builder.Services.AddNotificationsApplication();

        // MongoClient is thread-safe and pooled, so it is a singleton. The repository is scoped because it
        // uses ICurrentTenant.
        builder.Services.Configure<MongoOptions>(
            builder.Configuration.GetSection(MongoOptions.SectionName));
        builder.Services.AddSingleton<IMongoClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
            return new MongoClient(options.ConnectionString);
        });
        builder.Services.AddSingleton<IMongoDatabase>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
            return sp.GetRequiredService<IMongoClient>().GetDatabase(options.DatabaseName);
        });
        builder.Services.AddScoped<IActivityLogRepository, MongoActivityLogRepository>();

        builder.Services.AddScoped<ICvParseResultRepository, MongoCvParseResultRepository>();

        builder.Services.AddScoped<ICandidateSearchRepository, CandidateSearchRepository>();

        return builder;
    }
}
