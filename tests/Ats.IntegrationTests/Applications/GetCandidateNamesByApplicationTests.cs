using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;

namespace Ats.IntegrationTests.Applications;

[Collection("Integration")]
public sealed class GetCandidateNamesByApplicationTests
{
    private readonly PostgresContainerFixture _fixture;

    public GetCandidateNamesByApplicationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_resolve_candidate_names_for_known_applications_only()
    {
        // Arrange — a candidate with one application
        var tenant = new FixedTenant(Guid.NewGuid());
        await using var writeDb = new ApplicationsDbContext(
            PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);

        var candidate = Candidate.Create("jane@example.test", "Jane", "Doe");
        writeDb.Candidates.Add(candidate);
        var application = Application.Create(
            jobId: Guid.NewGuid(), candidateId: candidate.Id, candidateAccountId: null,
            initialStageId: Guid.NewGuid(), cvFileKey: "cv/jane.pdf");
        writeDb.Applications.Add(application);
        await writeDb.SaveChangesAsync();

        // Act — ask for the known id plus an unknown one
        await using var readDb = new ApplicationsDbContext(
            PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
        var unknownId = Guid.NewGuid();
        var names = await new ApplicationDirectory(readDb)
            .GetCandidateNamesByApplicationAsync(new[] { application.Id, unknownId });

        // Assert — the known application resolves to the full name; the unknown id is absent
        Assert.Equal("Jane Doe", names[application.Id]);
        Assert.False(names.ContainsKey(unknownId));
    }

    [Fact]
    public async Task should_return_empty_for_no_ids()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        await using var db = new ApplicationsDbContext(
            PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);

        var names = await new ApplicationDirectory(db).GetCandidateNamesByApplicationAsync([]);

        Assert.Empty(names);
    }
}
