using Ats.IntegrationTests.Shared;
using Ats.Modules.Jobs.Application;
using Ats.Modules.Jobs.Domain;
using Ats.Modules.Jobs.Infrastructure;

namespace Ats.IntegrationTests.Jobs;

// Covers the cross-module read the CV-parsing consumer depends on for job-fit analysis:
// IJobDirectory.GetJobRequirementsAsync must return the job's title/description for the given
// (tenantId, jobId) pair, scoped explicitly rather than through the ambient global filter (a
// message consumer has no resolved ICurrentTenant, the same reasoning as TenantDirectoryTests).
[Collection("Integration")]
public sealed class JobDirectoryRequirementsTests
{
    private readonly PostgresContainerFixture _fixture;

    public JobDirectoryRequirementsTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_return_the_title_and_description_for_the_given_tenant_and_job()
    {
        // Arrange
        var tenant = new FixedTenant(Guid.NewGuid());
        Guid jobId;
        await using (var db = NewDb(tenant))
        {
            var job = Job.Create(
                "Senior Backend Engineer", "Needs 5+ years of C# and PostgreSQL.", "Engineering",
                "Remote", null, EmploymentType.FullTime, ExperienceLevel.Senior, WorkArrangement.Remote,
                null, Guid.NewGuid());
            db.Jobs.Add(job);
            await db.SaveChangesAsync();
            jobId = job.Id;
        }

        // Act
        await using var readDb = NewDb(tenant);
        var requirements = await new JobDirectory(readDb).GetJobRequirementsAsync(tenant.TenantId!.Value, jobId);

        // Assert
        Assert.NotNull(requirements);
        Assert.Equal("Senior Backend Engineer", requirements.Title);
        Assert.Equal("Needs 5+ years of C# and PostgreSQL.", requirements.Description);
    }

    [Fact]
    public async Task should_return_null_when_the_tenant_id_does_not_own_the_job()
    {
        // Arrange
        var tenant = new FixedTenant(Guid.NewGuid());
        Guid jobId;
        await using (var db = NewDb(tenant))
        {
            var job = Job.Create(
                "Product Designer", "A description long enough to publish.", "Design", "Remote", null,
                EmploymentType.FullTime, ExperienceLevel.Mid, WorkArrangement.Remote, null, Guid.NewGuid());
            db.Jobs.Add(job);
            await db.SaveChangesAsync();
            jobId = job.Id;
        }

        // Act — a different tenant id entirely, as if a corrupted or malicious message claimed it
        await using var readDb = NewDb(tenant);
        var requirements = await new JobDirectory(readDb).GetJobRequirementsAsync(Guid.NewGuid(), jobId);

        // Assert
        Assert.Null(requirements);
    }

    [Fact]
    public async Task should_return_null_for_an_unknown_job()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        await using var db = NewDb(tenant);

        var requirements = await new JobDirectory(db).GetJobRequirementsAsync(
            tenant.TenantId!.Value, Guid.NewGuid());

        Assert.Null(requirements);
    }

    private JobsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
}
