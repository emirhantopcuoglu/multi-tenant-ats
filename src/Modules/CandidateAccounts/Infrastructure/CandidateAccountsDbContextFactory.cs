using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

// Design-time factory used by `dotnet ef` to build the context outside the running app (which is why
// it reads the connection string from an env var with a dev fallback, and takes no DI). Simpler than
// the other modules' factories: with no tenant filter there is no ICurrentTenant to stub out.
public sealed class CandidateAccountsDbContextFactory : IDesignTimeDbContextFactory<CandidateAccountsDbContext>
{
    public CandidateAccountsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ATS_DB_CONNECTION")
            ?? "Host=localhost;Port=5434;Database=ats;Username=ats;Password=ats_dev_password";

        var options = new DbContextOptionsBuilder<CandidateAccountsDbContext>()
            .UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "candidate_accounts"))
            .Options;

        return new CandidateAccountsDbContext(options);
    }
}
