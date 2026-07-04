using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Application;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;
using Ats.Shared.Contracts.Jobs;

namespace Ats.IntegrationTests.Applications;

[Collection("Integration")]
public sealed class GetForSchedulingTests
{
    private readonly PostgresContainerFixture _fixture;

    public GetForSchedulingTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_return_candidate_contact_and_job_title_for_scheduling()
    {
        // Arrange — one application with a real candidate behind it
        var tenant = new FixedTenant(Guid.NewGuid());
        var jobId = Guid.NewGuid();

        Candidate candidate;
        Application application;
        await using (var db = NewDb(tenant))
        {
            candidate = Candidate.Create("sched@acme.test", "Sked", "Uler");
            db.Candidates.Add(candidate);
            application = Application.Create(
                jobId, candidate.Id, Guid.NewGuid(), Guid.NewGuid(), "cv/sched.pdf");
            db.Applications.Add(application);
            await db.SaveChangesAsync();
        }

        // Act
        await using var readDb = NewDb(tenant);
        var directory = new ApplicationDirectory(
            readDb, new FakeJobDirectory(new JobSummary(jobId, "Platform Engineer", "platform-engineer", tenant.TenantId!.Value)));
        var result = await directory.GetForSchedulingAsync(application.Id);

        // Assert — everything the Interviews module needs to publish a self-contained event
        Assert.NotNull(result);
        Assert.Equal(application.Id, result.Id);
        Assert.True(result.IsActive);
        Assert.Equal(jobId, result.JobId);
        Assert.Equal("Platform Engineer", result.JobTitle);
        Assert.Equal(candidate.Id, result.CandidateId);
        Assert.Equal("sched@acme.test", result.CandidateEmail);
        Assert.Equal("Sked", result.CandidateFirstName);
    }

    [Fact]
    public async Task should_fall_back_to_empty_job_title_when_job_is_gone()
    {
        // Arrange — the job directory knows nothing about this job id (deleted role)
        var tenant = new FixedTenant(Guid.NewGuid());
        Application application;
        await using (var db = NewDb(tenant))
        {
            var candidate = Candidate.Create("orphan@acme.test", "Orph", "An");
            db.Candidates.Add(candidate);
            application = Application.Create(
                Guid.NewGuid(), candidate.Id, Guid.NewGuid(), Guid.NewGuid(), "cv/orphan.pdf");
            db.Applications.Add(application);
            await db.SaveChangesAsync();
        }

        // Act
        await using var readDb = NewDb(tenant);
        var result = await new ApplicationDirectory(readDb, new FakeJobDirectory(null))
            .GetForSchedulingAsync(application.Id);

        // Assert — empty, not null: consumers render their own "the role you applied for" fallback
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.JobTitle);
    }

    private ApplicationsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
}
