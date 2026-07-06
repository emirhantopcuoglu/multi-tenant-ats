using Ats.IntegrationTests.Shared;
using Ats.Modules.Jobs.Application.Jobs;
using Ats.Modules.Jobs.Domain;
using Ats.Modules.Jobs.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Ats.IntegrationTests.Jobs;

[Collection("Integration")]
public sealed class CreateJobHandlerTests
{
    private readonly PostgresContainerFixture _fixture;

    public CreateJobHandlerTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_persist_job_and_return_id()
    {
        // Arrange
        var tenant = new FixedTenant(Guid.NewGuid());
        await using var db = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);

        var handler = new CreateJobHandler(db);
        var command = new CreateJobCommand(
            "Senior Engineer", "Design and build systems", "Engineering", "Remote", null,
            EmploymentType.FullTime, ExperienceLevel.Senior, WorkArrangement.Remote, null, null, null, Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — verify the returned ID resolves to a persisted row via a second context
        Assert.True(result.IsSuccess);

        await using var readDb = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
        var saved = await readDb.Jobs.FirstOrDefaultAsync(j => j.Id == result.Value);

        Assert.NotNull(saved);
        Assert.Equal("Senior Engineer", saved.Title);
        Assert.Equal(JobStatus.Draft, saved.Status);
    }

    [Fact]
    public async Task should_stamp_tenant_id_via_interceptor()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var tenant = new FixedTenant(tenantId);
        await using var db = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);

        var handler = new CreateJobHandler(db);
        var command = new CreateJobCommand(
            "Product Manager", "Own the product roadmap", "Product", "New York", null,
            EmploymentType.FullTime, ExperienceLevel.Mid, WorkArrangement.OnSite, null, null, null, Guid.NewGuid());

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — TenantSaveChangesInterceptor must have stamped TenantId on the entity
        Assert.True(result.IsSuccess);

        await using var readDb = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
        var saved = await readDb.Jobs.FirstOrDefaultAsync(j => j.Id == result.Value);

        Assert.NotNull(saved);
        Assert.Equal(tenantId, saved.TenantId);
    }
}
