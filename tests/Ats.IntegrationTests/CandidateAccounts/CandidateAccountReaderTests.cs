using Ats.IntegrationTests.Shared;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Shared.Contracts.CandidateAccounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ats.IntegrationTests.CandidateAccounts;

[Collection("Integration")]
public sealed class CandidateAccountReaderTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;

    public CandidateAccountReaderTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using var db = new CandidateAccountsDbContext(
            PostgresContainerFixture.BuildCandidateAccountsOptions(_fixture.ConnectionString));
        await db.Database.ExecuteSqlRawAsync("DELETE FROM candidate_accounts.\"CandidateAccounts\"");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Returns_summary_for_an_existing_account()
    {
        // Arrange
        await using var db = CreateDb();
        var hasher = new PasswordHasher<CandidateAccount>();
        var account = CandidateAccount.Register("alice@example.com", hasher.HashPassword(null!, "pass"), "Alice", "Smith");
        db.CandidateAccounts.Add(account);
        await db.SaveChangesAsync();

        // Act
        var reader = new CandidateAccountReader(db);
        var summary = await reader.GetByIdAsync(account.Id);

        // Assert
        Assert.NotNull(summary);
        Assert.Equal(account.Id, summary.Id);
        Assert.Equal("alice@example.com", summary.Email);
        Assert.Equal("Alice", summary.FirstName);
        Assert.Equal("Smith", summary.LastName);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_id()
    {
        // Arrange — no accounts in the table
        await using var db = CreateDb();
        var reader = new CandidateAccountReader(db);

        // Act
        var summary = await reader.GetByIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(summary);
    }

    private CandidateAccountsDbContext CreateDb() =>
        new(PostgresContainerFixture.BuildCandidateAccountsOptions(_fixture.ConnectionString));
}
