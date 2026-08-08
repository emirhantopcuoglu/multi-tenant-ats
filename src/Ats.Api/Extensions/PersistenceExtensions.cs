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
    // Every module's PostgreSQL DbContext, its module-specific Application DI, and the shared
    // MongoDB stores. Kept as one method rather than one-per-module because none of these have an
    // independent reason to change on their own — a new module always brings both a DbContext and
    // its Add<Module>Application() call together.
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

        // Candidate accounts (FAZ 7): the marketplace's global, tenant-less identity. Unlike every other
        // context, this one takes no tenant/audit interceptors — its only entity is neither ITenantScoped
        // nor IAuditable, so those interceptors would be inert. Registered here (deferred from 7.3) now
        // that the auth services below consume it.
        builder.Services.AddDbContext<CandidateAccountsDbContext>(options =>
            options.UseNpgsql(
                builder.Configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "candidate_accounts")));

        // In-app notifications (FAZ 3). Like candidate accounts, no tenant/audit interceptors: a
        // Notification row is addressed to a recipient — a global candidate account today, a company user
        // later — and the recipient, not a tenant, is the ownership boundary its queries filter on.
        builder.Services.AddDbContext<NotificationsDbContext>(options =>
            options.UseNpgsql(
                builder.Configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "notifications")));

        builder.Services.AddScoped<INotificationsDbContext>(sp => sp.GetRequiredService<NotificationsDbContext>());
        builder.Services.AddNotificationsApplication();

        // MongoDB holds the append-only activity log (Sprint 4). The driver's MongoClient is thread-safe
        // and pools connections internally, so it is a singleton; the database handle is derived from it.
        // The repository is scoped because it depends on the per-request ICurrentTenant for isolation.
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

        // CV parse results (Sprint 6.3) also live in MongoDB. Scoped like the activity log because the
        // read path depends on the per-request ICurrentTenant; the write path (the CV-parsing consumer)
        // passes the tenant explicitly, since it runs outside a resolved-tenant request.
        builder.Services.AddScoped<ICvParseResultRepository, MongoCvParseResultRepository>();

        // Candidate full-text search (Sprint 6.4). Backed by a PostgreSQL tsvector generated column on
        // the Candidates table; the repository is scoped because the underlying DbContext is scoped.
        builder.Services.AddScoped<ICandidateSearchRepository, CandidateSearchRepository>();

        return builder;
    }
}
