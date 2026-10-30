using Ats.Modules.Applications.Infrastructure;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Modules.Interviews.Infrastructure;
using Ats.Modules.Jobs.Infrastructure;
using Ats.Modules.Notifications.Infrastructure;
using Ats.Modules.Tenants.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Ats.Api;

public static class DatabaseMigrator
{
    public const string Command = "migrate";

    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseMigrator));

        await MigrateAsync<TenantsDbContext>(provider, logger, cancellationToken);
        await MigrateAsync<JobsDbContext>(provider, logger, cancellationToken);
        await MigrateAsync<ApplicationsDbContext>(provider, logger, cancellationToken);
        await MigrateAsync<InterviewsDbContext>(provider, logger, cancellationToken);
        await MigrateAsync<CandidateAccountsDbContext>(provider, logger, cancellationToken);
        await MigrateAsync<NotificationsDbContext>(provider, logger, cancellationToken);
    }

    private static async Task MigrateAsync<TContext>(
        IServiceProvider provider, ILogger logger, CancellationToken cancellationToken)
        where TContext : DbContext
    {
        var db = provider.GetRequiredService<TContext>();
        var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

        logger.LogInformation("{Context}: {Count} pending migration(s)", typeof(TContext).Name, pending.Count);
        await db.Database.MigrateAsync(cancellationToken);
    }
}
