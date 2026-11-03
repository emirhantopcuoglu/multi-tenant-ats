using Ats.IntegrationTests.Shared;
using Ats.Modules.Jobs.Application.Jobs;
using Ats.Modules.Jobs.Domain;
using Ats.Modules.Jobs.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.IntegrationTests.Jobs;

// The stale copy lives in a browser form, so each save here runs in a fresh context and only the
// version it sends says what the editor saw.
[Collection("Integration")]
public sealed class JobEditConcurrencyTests
{
    private readonly PostgresContainerFixture _fixture;

    public JobEditConcurrencyTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_refuse_a_save_from_a_form_loaded_before_someone_else_saved()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var jobId = await SeedJobAsync(tenant);
        var loadedByBoth = await GetAsync(tenant, jobId);

        var first = await UpdateAsync(tenant, jobId, "Senior Backend Developer", loadedByBoth.Version);
        var second = () => UpdateAsync(tenant, jobId, "Backend Engineer", loadedByBoth.Version);

        Assert.True(first.IsSuccess);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(second);
        Assert.Equal("Senior Backend Developer", (await GetAsync(tenant, jobId)).Title);
    }

    [Fact]
    public async Task should_save_again_with_the_version_returned_by_a_fresh_load()
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        var jobId = await SeedJobAsync(tenant);

        var first = await UpdateAsync(tenant, jobId, "Senior Backend Developer", (await GetAsync(tenant, jobId)).Version);
        var second = await UpdateAsync(tenant, jobId, "Backend Engineer", (await GetAsync(tenant, jobId)).Version);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("Backend Engineer", (await GetAsync(tenant, jobId)).Title);
    }

    private async Task<JobDetailDto> GetAsync(FixedTenant tenant, Guid jobId)
    {
        await using var db = NewDb(tenant);
        var result = await new GetJobByIdHandler(db).Handle(new GetJobByIdQuery(jobId), CancellationToken.None);
        return result.Value;
    }

    private async Task<Result<bool>> UpdateAsync(FixedTenant tenant, Guid jobId, string title, uint version)
    {
        await using var db = NewDb(tenant);
        var command = new UpdateJobCommand(
            jobId, title, "Build APIs", "Engineering", "Istanbul", "Turkey",
            EmploymentType.FullTime, ExperienceLevel.Mid, WorkArrangement.Remote,
            null, null, null, version);
        return await new UpdateJobHandler(db).Handle(command, CancellationToken.None);
    }

    private async Task<Guid> SeedJobAsync(FixedTenant tenant)
    {
        await using var db = NewDb(tenant);
        var job = Job.Create(
            "Backend Developer", "Build APIs", "Engineering", "Istanbul", "Turkey",
            EmploymentType.FullTime, ExperienceLevel.Mid, WorkArrangement.Remote, null, Guid.NewGuid());
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        return job.Id;
    }

    private JobsDbContext NewDb(FixedTenant tenant) =>
        new(PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
}
