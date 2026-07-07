using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Application.Applications;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;

namespace Ats.IntegrationTests.Applications;

[Collection("Integration")]
public sealed class ListCandidateAppliedJobIdsTests
{
    private readonly PostgresContainerFixture _fixture;

    public ListCandidateAppliedJobIdsTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_return_only_active_job_ids_for_the_account_across_tenants()
    {
        // Arrange — one account with: an active application (in), a rejected one (out — the
        // duplicate rule allows re-applying after rejection), a withdrawn one (out), and an
        // active application in a second tenant (in — the account is the scope, not the tenant).
        // A different account's active application must not leak in.
        var accountId = Guid.NewGuid();
        var otherAccountId = Guid.NewGuid();
        var activeJob = Guid.NewGuid();
        var rejectedJob = Guid.NewGuid();
        var withdrawnJob = Guid.NewGuid();
        var otherTenantJob = Guid.NewGuid();
        var otherAccountJob = Guid.NewGuid();

        var tenant = new FixedTenant(Guid.NewGuid());
        await using (var db = NewDb(tenant))
        {
            var candidate = AddCandidate(db, "applied@acme.test");
            db.Applications.Add(NewApplication(activeJob, candidate.Id, accountId));

            var rejected = NewApplication(rejectedJob, candidate.Id, accountId);
            rejected.Reject("Not a fit.", Guid.NewGuid());
            db.Applications.Add(rejected);

            var withdrawn = NewApplication(withdrawnJob, candidate.Id, accountId);
            withdrawn.Withdraw();
            db.Applications.Add(withdrawn);

            var otherCandidate = AddCandidate(db, "other@acme.test");
            db.Applications.Add(NewApplication(otherAccountJob, otherCandidate.Id, otherAccountId));

            await db.SaveChangesAsync();
        }

        var otherTenant = new FixedTenant(Guid.NewGuid());
        await using (var db = NewDb(otherTenant))
        {
            var candidate = AddCandidate(db, "applied@globex.test");
            db.Applications.Add(NewApplication(otherTenantJob, candidate.Id, accountId));
            await db.SaveChangesAsync();
        }

        // Act — read under an unrelated tenant to prove the handler ignores the tenant filter.
        await using var readDb = NewDb(new FixedTenant(Guid.NewGuid()));
        var result = await new ListCandidateAppliedJobIdsHandler(readDb)
            .Handle(new ListCandidateAppliedJobIdsQuery(accountId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(
            new[] { activeJob, otherTenantJob }.OrderBy(id => id),
            result.Value.OrderBy(id => id));
    }

    [Fact]
    public async Task should_return_empty_when_account_has_no_applications()
    {
        // Arrange
        await using var readDb = NewDb(new FixedTenant(Guid.NewGuid()));

        // Act
        var result = await new ListCandidateAppliedJobIdsHandler(readDb)
            .Handle(new ListCandidateAppliedJobIdsQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    private static Candidate AddCandidate(ApplicationsDbContext db, string email)
    {
        var candidate = Candidate.Create(email, "Test", "Candidate");
        db.Candidates.Add(candidate);
        return candidate;
    }

    private static Application NewApplication(Guid jobId, Guid candidateId, Guid accountId) =>
        Application.Create(
            jobId: jobId, candidateId: candidateId, candidateAccountId: accountId,
            initialStageId: Guid.NewGuid(), cvFileKey: "cv/test.pdf");

    private ApplicationsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
}
