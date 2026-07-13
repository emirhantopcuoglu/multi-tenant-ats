using Ats.IntegrationTests.Shared;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Ats.IntegrationTests.CandidateAccounts;

// Same hygiene as CandidateAuthServiceTests: CandidateAccount is a global (tenant-less) table, so the
// rows are wiped before each test to keep the runs deterministic.
[Collection("Integration")]
public sealed class CandidateProfileServiceTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;

    public CandidateProfileServiceTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM candidate_accounts.\"CandidateAccounts\"");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Update_should_persist_the_full_profile_and_return_it()
    {
        // Arrange
        var accountId = await SeedAccountAsync();
        var service = CreateService();
        var birthDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-30);

        // Act
        var updated = await service.UpdateAsync(accountId, new UpdateCandidateProfileCommand(
            "Janet", "Roe", "+90 532 123 45 67", "Turkey", "Istanbul", birthDate));

        // Assert — the write is visible both in the immediate result and on a fresh read
        Assert.True(updated.IsSuccess);
        Assert.Equal("Janet", updated.Value.FirstName);
        Assert.Equal("+905321234567", updated.Value.PhoneNumber);

        var fresh = await CreateService().GetAsync(accountId);
        Assert.True(fresh.IsSuccess);
        Assert.Equal("Roe", fresh.Value.LastName);
        Assert.Equal("Turkey", fresh.Value.Country);
        Assert.Equal("Istanbul", fresh.Value.City);
        Assert.Equal(birthDate, fresh.Value.BirthDate);
    }

    [Fact]
    public async Task Update_should_fail_for_an_unknown_candidate()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateCandidateProfileCommand(
            "Janet", "Roe", null, null, null, null));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CandidateProfileErrors.NotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Update_should_reject_a_location_outside_the_supported_catalogue()
    {
        // Arrange
        var accountId = await SeedAccountAsync();
        var service = CreateService();

        // Act — a real city, but not one belonging to the selected country
        var result = await service.UpdateAsync(accountId, new UpdateCandidateProfileCommand(
            "Jane", "Doe", null, "Germany", "Istanbul", null));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CandidateProfileErrors.UnsupportedLocation.Code, result.Error.Code);
    }

    [Fact]
    public async Task Update_should_translate_a_domain_invariant_violation_into_a_typed_error()
    {
        // Arrange
        var accountId = await SeedAccountAsync();
        var service = CreateService();

        // Act — an invalid phone trips the domain guard, which must surface as a result, not a throw
        var result = await service.UpdateAsync(accountId, new UpdateCandidateProfileCommand(
            "Jane", "Doe", "not-a-phone", null, null, null));

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal(CandidateProfileErrors.InvalidData("any").Code, result.Error.Code);
    }

    private CandidateAccountsDbContext CreateDbContext() =>
        new(PostgresContainerFixture.BuildCandidateAccountsOptions(_fixture.ConnectionString));

    private CandidateProfileService CreateService() => new(CreateDbContext());

    private async Task<Guid> SeedAccountAsync()
    {
        await using var db = CreateDbContext();
        var account = CandidateAccount.Register("jane@example.com", "hashed-password", "Jane", "Doe");
        db.CandidateAccounts.Add(account);
        await db.SaveChangesAsync();
        return account.Id;
    }
}
