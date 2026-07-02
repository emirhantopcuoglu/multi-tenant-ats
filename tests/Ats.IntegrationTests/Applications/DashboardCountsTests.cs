using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;

namespace Ats.IntegrationTests.Applications;

[Collection("Integration")]
public sealed class DashboardCountsTests
{
    private readonly PostgresContainerFixture _fixture;

    public DashboardCountsTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_count_distinct_active_candidates_scoped_to_tenant()
    {
        // Arrange — candidate A: two active applications (must count once); candidate B: one active;
        // candidate C: one rejected (excluded). Another tenant has an active candidate that must not leak.
        var tenant = new FixedTenant(Guid.NewGuid());
        await using (var db = NewDb(tenant))
        {
            var candidateA = AddCandidate(db, "a@acme.test");
            db.Applications.Add(NewApplication(candidateA.Id));
            db.Applications.Add(NewApplication(candidateA.Id));

            var candidateB = AddCandidate(db, "b@acme.test");
            db.Applications.Add(NewApplication(candidateB.Id));

            var candidateC = AddCandidate(db, "c@acme.test");
            var rejected = NewApplication(candidateC.Id);
            rejected.Reject("Not a fit.");
            db.Applications.Add(rejected);

            await db.SaveChangesAsync();
        }

        var otherTenant = new FixedTenant(Guid.NewGuid());
        await using (var db = NewDb(otherTenant))
        {
            var outsider = AddCandidate(db, "x@globex.test");
            db.Applications.Add(NewApplication(outsider.Id));
            await db.SaveChangesAsync();
        }

        // Act
        await using var readDb = NewDb(tenant);
        var count = await new ApplicationDirectory(readDb).CountActiveCandidatesAsync();

        // Assert — candidates A and B; the rejected one and the other tenant are excluded
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task should_count_applications_within_the_window()
    {
        // Arrange — three applications, all submitted "now" (AppliedAtUtc is set at creation).
        var tenant = new FixedTenant(Guid.NewGuid());
        await using (var db = NewDb(tenant))
        {
            var candidate = AddCandidate(db, "win@acme.test");
            db.Applications.Add(NewApplication(candidate.Id));
            db.Applications.Add(NewApplication(candidate.Id));
            db.Applications.Add(NewApplication(candidate.Id));
            await db.SaveChangesAsync();
        }

        await using var readDb = NewDb(tenant);
        var directory = new ApplicationDirectory(readDb);

        // Act + Assert — a window opening in the past captures all three; one opening in the future none.
        Assert.Equal(3, await directory.CountApplicationsSinceAsync(DateTime.UtcNow.AddDays(-1)));
        Assert.Equal(0, await directory.CountApplicationsSinceAsync(DateTime.UtcNow.AddDays(1)));
    }

    private static Candidate AddCandidate(ApplicationsDbContext db, string email)
    {
        var candidate = Candidate.Create(email, "Test", "Candidate");
        db.Candidates.Add(candidate);
        return candidate;
    }

    private static Application NewApplication(Guid candidateId) =>
        Application.Create(
            jobId: Guid.NewGuid(), candidateId: candidateId, candidateAccountId: null,
            initialStageId: Guid.NewGuid(), cvFileKey: "cv/test.pdf");

    private ApplicationsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
}
