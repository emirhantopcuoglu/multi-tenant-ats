using Ats.IntegrationTests.Shared;
using Ats.Modules.Jobs.Application.Jobs;
using Ats.Modules.Jobs.Domain;
using Ats.Modules.Jobs.Infrastructure;

namespace Ats.IntegrationTests.Jobs;

[Collection("Integration")]
public sealed class GetJobByIdHandlerTests
{
    private readonly PostgresContainerFixture _fixture;

    public GetJobByIdHandlerTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_return_description_and_salary_for_the_edit_form()
    {
        // Arrange — persist a job that carries a salary range
        var tenant = new FixedTenant(Guid.NewGuid());
        await using var writeDb = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);

        var job = Job.Create(
            "Staff Engineer", "Lead the platform team", "Engineering", "Remote", null,
            EmploymentType.FullTime, ExperienceLevel.Lead, WorkArrangement.Remote,
            new SalaryRange(120000m, 160000m, "usd"), Guid.NewGuid());
        writeDb.Jobs.Add(job);
        await writeDb.SaveChangesAsync();

        // Act — read it back through the detail handler on a fresh context
        await using var readDb = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
        var result = await new GetJobByIdHandler(readDb).Handle(new GetJobByIdQuery(job.Id), CancellationToken.None);

        // Assert — the detail DTO exposes the fields the list DTO omits
        Assert.True(result.IsSuccess);
        Assert.Equal("Lead the platform team", result.Value.Description);
        Assert.Equal(120000m, result.Value.SalaryMin);
        Assert.Equal(160000m, result.Value.SalaryMax);
        Assert.Equal("USD", result.Value.SalaryCurrency); // normalized to upper-case by SalaryRange
    }

    [Fact]
    public async Task should_return_null_salary_when_none_was_set()
    {
        // Arrange — a job without a salary range (optional owned type → null columns)
        var tenant = new FixedTenant(Guid.NewGuid());
        await using var writeDb = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);

        var job = Job.Create(
            "Recruiter", "Run hiring pipelines", "People", "Istanbul", "Turkey",
            EmploymentType.FullTime, ExperienceLevel.Mid, WorkArrangement.OnSite, salaryRange: null, createdBy: Guid.NewGuid());
        writeDb.Jobs.Add(job);
        await writeDb.SaveChangesAsync();

        // Act
        await using var readDb = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
        var result = await new GetJobByIdHandler(readDb).Handle(new GetJobByIdQuery(job.Id), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.SalaryMin);
        Assert.Null(result.Value.SalaryMax);
        Assert.Null(result.Value.SalaryCurrency);
    }
}
