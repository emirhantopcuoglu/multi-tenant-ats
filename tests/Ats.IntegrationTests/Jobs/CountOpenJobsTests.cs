using Ats.IntegrationTests.Shared;
using Ats.Modules.Jobs.Application;
using Ats.Modules.Jobs.Domain;
using Ats.Modules.Jobs.Infrastructure;

namespace Ats.IntegrationTests.Jobs;

[Collection("Integration")]
public sealed class CountOpenJobsTests
{
    private readonly PostgresContainerFixture _fixture;

    public CountOpenJobsTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_count_only_published_jobs_scoped_to_tenant()
    {
        // Arrange — target tenant: two published + one draft; another tenant has a published job that
        // must not be counted. Each test uses a fresh tenant id, so the query filter isolates it.
        var tenant = new FixedTenant(Guid.NewGuid());
        await using (var db = NewDb(tenant))
        {
            db.Jobs.Add(PublishedJob("Senior Backend Engineer"));
            db.Jobs.Add(PublishedJob("Product Designer"));
            db.Jobs.Add(Job.Create(
                "Draft Role", "desc", "Eng", "Remote", null,
                EmploymentType.FullTime, ExperienceLevel.Senior, WorkArrangement.Remote, null, Guid.NewGuid()));
            await db.SaveChangesAsync();
        }

        var otherTenant = new FixedTenant(Guid.NewGuid());
        await using (var db = NewDb(otherTenant))
        {
            db.Jobs.Add(PublishedJob("Other Tenant Role"));
            await db.SaveChangesAsync();
        }

        // Act
        await using var readDb = NewDb(tenant);
        var count = await new JobDirectory(readDb).CountOpenJobsAsync();

        // Assert — only the two published jobs in the target tenant
        Assert.Equal(2, count);
    }

    private static Job PublishedJob(string title)
    {
        var job = Job.Create(
            title, "A description long enough to publish.", "Engineering", "Remote", null,
            EmploymentType.FullTime, ExperienceLevel.Senior, WorkArrangement.Remote, null, Guid.NewGuid());
        job.Publish();
        return job;
    }

    private JobsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
}
