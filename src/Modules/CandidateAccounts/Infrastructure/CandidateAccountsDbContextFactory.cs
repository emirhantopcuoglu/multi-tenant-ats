using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ats.Modules.CandidateAccounts.Infrastructure;

// Used by dotnet ef at design time.
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
