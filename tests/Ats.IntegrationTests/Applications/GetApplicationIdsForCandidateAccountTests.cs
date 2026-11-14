using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;

namespace Ats.IntegrationTests.Applications;

[Collection("Integration")]
public sealed class GetApplicationIdsForCandidateAccountTests
{
    private readonly PostgresContainerFixture _fixture;

    public GetApplicationIdsForCandidateAccountTests(PostgresContainerFixture fixture) => _fixture = fixture;

    // Called with no tenant by the account erasure, which has to reach every company.
    [Fact]
    public async Task should_find_the_accounts_applications_in_every_company_deleted_ones_included()
    {
        var accountId = Guid.NewGuid();
        var first = await ApplyAsync(Guid.NewGuid(), accountId);
        var second = await ApplyAsync(Guid.NewGuid(), accountId);
        var deleted = await ApplyAsync(Guid.NewGuid(), accountId, delete: true);
        await ApplyAsync(Guid.NewGuid(), Guid.NewGuid());

        await using var db = NewDb(tenantId: null);
        var found = await new ApplicationDirectory(db, new FakeJobDirectory(null))
            .GetApplicationIdsForCandidateAccountAsync(accountId);

        Assert.Equal(new[] { first, second, deleted }.Order(), found.Order());
    }

    private async Task<Guid> ApplyAsync(Guid tenantId, Guid accountId, bool delete = false)
    {
        await using var db = NewDb(tenantId);
        var candidate = Candidate.Create($"{Guid.NewGuid():N}@example.test", "Mira", "Tolun");
        var application = Application.Create(Guid.NewGuid(), candidate.Id, accountId, Guid.NewGuid(), "cv/mira.pdf");
        db.Candidates.Add(candidate);
        db.Applications.Add(application);
        await db.SaveChangesAsync();

        if (delete)
        {
            db.Applications.Remove(application);
            await db.SaveChangesAsync();
        }

        return application.Id;
    }

    private ApplicationsDbContext NewDb(Guid? tenantId)
    {
        var tenant = new FixedTenant(tenantId);
        return new ApplicationsDbContext(
            PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
    }
}
