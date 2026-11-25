using System.Data.Common;
using Ats.Modules.Applications.Infrastructure;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Modules.Interviews.Infrastructure;
using Ats.Modules.Jobs.Infrastructure;
using Ats.Modules.Notifications.Infrastructure;
using Ats.Modules.Tenants.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ats.Api;

// Every module's pending migrations run as one script in one transaction, so a failure in any of them
// leaves the database as it was. Migrate() can't do that: it opens its own transaction per module.
public static class DatabaseMigrator
{
    public const string Command = "migrate";

    // Two migrators at once would both see the same migrations as pending. Any id works as long as nothing
    // else in the database uses it.
    private const string LockSql = "SELECT pg_advisory_xact_lock(7301202611)";

    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseMigrator));

        DbContext[] contexts =
        [
            provider.GetRequiredService<TenantsDbContext>(),
            provider.GetRequiredService<JobsDbContext>(),
            provider.GetRequiredService<ApplicationsDbContext>(),
            provider.GetRequiredService<InterviewsDbContext>(),
            provider.GetRequiredService<CandidateAccountsDbContext>(),
            provider.GetRequiredService<NotificationsDbContext>()
        ];

        var connection = contexts[0].Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await ExecuteAsync(connection, transaction, LockSql, cancellationToken);

        foreach (var db in contexts)
        {
            if (db != contexts[0])
                db.Database.SetDbConnection(connection);
            await db.Database.UseTransactionAsync(transaction, cancellationToken);

            var history = db.GetService<IHistoryRepository>();
            await ExecuteAsync(connection, transaction, history.GetCreateIfNotExistsScript(), cancellationToken);

            var applied = (await history.GetAppliedMigrationsAsync(cancellationToken)).Select(m => m.MigrationId).ToList();
            var pending = db.Database.GetMigrations().Except(applied).ToList();
            logger.LogInformation("{Context}: {Count} pending migration(s)", db.GetType().Name, pending.Count);
            if (pending.Count == 0)
                continue;

            var script = db.GetService<IMigrator>().GenerateScript(
                fromMigration: applied.LastOrDefault() ?? Migration.InitialDatabase,
                options: MigrationsSqlGenerationOptions.NoTransactions);
            await ExecuteAsync(connection, transaction, script, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(
        DbConnection connection, DbTransaction transaction, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.CommandTimeout = 0;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
