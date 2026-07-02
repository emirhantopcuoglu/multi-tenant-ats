using Ats.IntegrationTests.Shared;
using Ats.Modules.Applications.Application.Applications;
using Ats.Modules.Applications.Domain;
using Ats.Modules.Applications.Infrastructure;

namespace Ats.IntegrationTests.Applications;

[Collection("Integration")]
public sealed class ListPipelineStagesHandlerTests
{
    private readonly PostgresContainerFixture _fixture;

    public ListPipelineStagesHandlerTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_return_default_stages_in_order()
    {
        // Arrange — seed a job's default pipeline (6 stages, orders 1..6)
        var tenant = new FixedTenant(Guid.NewGuid());
        var jobId = Guid.NewGuid();
        await using var writeDb = new ApplicationsDbContext(
            PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
        writeDb.Pipelines.Add(Pipeline.CreateDefault(jobId));
        await writeDb.SaveChangesAsync();

        // Act
        await using var readDb = new ApplicationsDbContext(
            PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);
        var result = await new ListPipelineStagesHandler(readDb)
            .Handle(new ListPipelineStagesQuery(jobId), CancellationToken.None);

        // Assert — the default funnel, in order
        Assert.True(result.IsSuccess);
        Assert.Equal(
            new[] { "Applied", "Screening", "Interview", "Offer", "Hired", "Rejected" },
            result.Value.Select(s => s.Name).ToArray());
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, result.Value.Select(s => s.Order).ToArray());
    }

    [Fact]
    public async Task should_return_empty_when_job_has_no_pipeline()
    {
        // Arrange — a tenant with no pipeline for the requested job
        var tenant = new FixedTenant(Guid.NewGuid());
        await using var readDb = new ApplicationsDbContext(
            PostgresContainerFixture.BuildApplicationsOptions(_fixture.ConnectionString, tenant), tenant);

        // Act
        var result = await new ListPipelineStagesHandler(readDb)
            .Handle(new ListPipelineStagesQuery(Guid.NewGuid()), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }
}
