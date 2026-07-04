using Ats.IntegrationTests.Shared;
using Ats.Modules.Jobs.Application.Jobs;
using Ats.Modules.Jobs.Domain;
using Ats.Modules.Jobs.Infrastructure;

namespace Ats.IntegrationTests.Jobs;

[Collection("Integration")]
public sealed class GetPublicJobBySlugHandlerTests
{
    private readonly PostgresContainerFixture _fixture;

    public GetPublicJobBySlugHandlerTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_return_published_job_with_description_and_salary_by_slug()
    {
        // Arrange — a published job carrying a salary range
        var tenant = new FixedTenant(Guid.NewGuid());
        await using var writeDb = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);

        var job = Job.Create(
            "Staff Engineer", "Lead the platform team", "Engineering", "Remote",
            EmploymentType.FullTime, ExperienceLevel.Lead,
            new SalaryRange(120000m, 160000m, "usd"), Guid.NewGuid());
        job.Publish();
        writeDb.Jobs.Add(job);
        await writeDb.SaveChangesAsync();

        // Act — read it back through the public handler by slug
        await using var readDb = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
        var result = await new GetPublicJobBySlugHandler(readDb)
            .Handle(new GetPublicJobBySlugQuery(job.Slug), CancellationToken.None);

        // Assert — the public detail exposes the full content the careers page renders
        Assert.True(result.IsSuccess);
        Assert.Equal(job.Slug, result.Value.Slug);
        Assert.Equal("Lead the platform team", result.Value.Description);
        Assert.Equal(120000m, result.Value.SalaryMin);
        Assert.Equal("USD", result.Value.SalaryCurrency);
        Assert.NotNull(result.Value.PublishedAtUtc);
    }

    [Fact]
    public async Task should_return_not_found_for_a_draft_job()
    {
        // Arrange — a job left in Draft (never published) must stay invisible to the public
        var tenant = new FixedTenant(Guid.NewGuid());
        await using var writeDb = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);

        var job = Job.Create(
            "Secret Role", "Not announced yet", "People", "Istanbul",
            EmploymentType.FullTime, ExperienceLevel.Mid, salaryRange: null, Guid.NewGuid());
        writeDb.Jobs.Add(job);
        await writeDb.SaveChangesAsync();

        // Act
        await using var readDb = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
        var result = await new GetPublicJobBySlugHandler(readDb)
            .Handle(new GetPublicJobBySlugQuery(job.Slug), CancellationToken.None);

        // Assert — a draft reads as not found, never as an empty-but-200 leak
        Assert.False(result.IsSuccess);
        Assert.Equal(JobErrors.NotFound.Code, result.Error.Code);
    }
}
