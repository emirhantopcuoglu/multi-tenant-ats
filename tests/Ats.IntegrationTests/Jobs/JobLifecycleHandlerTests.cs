using Ats.IntegrationTests.Shared;
using Ats.Modules.Jobs.Application.Jobs;
using Ats.Modules.Jobs.Domain;
using Ats.Modules.Jobs.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Ats.IntegrationTests.Jobs;

[Collection("Integration")]
public sealed class JobLifecycleHandlerTests
{
    private readonly PostgresContainerFixture _fixture;

    public JobLifecycleHandlerTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_publish_draft_job()
    {
        // Arrange
        var tenant = new FixedTenant(Guid.NewGuid());
        var jobId = await SeedDraftJobAsync(tenant);

        await using var db = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
        var handler = new PublishJobHandler(db);

        // Act
        var result = await handler.Handle(new PublishJobCommand(jobId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        await using var readDb = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
        var job = await readDb.Jobs.FirstAsync(j => j.Id == jobId);

        Assert.Equal(JobStatus.Published, job.Status);
        Assert.NotNull(job.PublishedAtUtc);
    }

    [Fact]
    public async Task should_fail_to_publish_already_published_job()
    {
        // Arrange — publish once successfully
        var tenant = new FixedTenant(Guid.NewGuid());
        var jobId = await SeedDraftJobAsync(tenant);

        await using var db1 = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
        await new PublishJobHandler(db1).Handle(new PublishJobCommand(jobId), CancellationToken.None);

        // Act — try to publish again
        await using var db2 = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
        var result = await new PublishJobHandler(db2).Handle(new PublishJobCommand(jobId), CancellationToken.None);

        // Assert — domain rule: only Draft can be published
        Assert.False(result.IsSuccess);
        Assert.Equal("job.invalid_operation", result.Error.Code);
    }

    [Fact]
    public async Task should_close_published_job()
    {
        // Arrange
        var tenant = new FixedTenant(Guid.NewGuid());
        var jobId = await SeedDraftJobAsync(tenant);

        await using var publishDb = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
        await new PublishJobHandler(publishDb).Handle(new PublishJobCommand(jobId), CancellationToken.None);

        // Act
        await using var closeDb = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
        var result = await new CloseJobHandler(closeDb).Handle(new CloseJobCommand(jobId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        await using var readDb = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
        var job = await readDb.Jobs.FirstAsync(j => j.Id == jobId);

        Assert.Equal(JobStatus.Closed, job.Status);
        Assert.NotNull(job.ClosedAtUtc);
    }

    [Fact]
    public async Task should_archive_closed_job()
    {
        // Arrange — drive the job to Closed state
        var tenant = new FixedTenant(Guid.NewGuid());
        var jobId = await SeedDraftJobAsync(tenant);

        await using var publishDb = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
        await new PublishJobHandler(publishDb).Handle(new PublishJobCommand(jobId), CancellationToken.None);

        await using var closeDb = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
        await new CloseJobHandler(closeDb).Handle(new CloseJobCommand(jobId), CancellationToken.None);

        // Act
        await using var archiveDb = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
        var result = await new ArchiveJobHandler(archiveDb).Handle(new ArchiveJobCommand(jobId), CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);

        await using var readDb = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
        var job = await readDb.Jobs.FirstAsync(j => j.Id == jobId);

        Assert.Equal(JobStatus.Archived, job.Status);
    }

    [Fact]
    public async Task should_return_not_found_for_unknown_job()
    {
        // Arrange
        var tenant = new FixedTenant(Guid.NewGuid());

        await using var db = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
        var handler = new PublishJobHandler(db);

        // Act
        var result = await handler.Handle(new PublishJobCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("job.not_found", result.Error.Code);
    }

    private async Task<Guid> SeedDraftJobAsync(FixedTenant tenant)
    {
        await using var db = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);

        var job = Job.Create(
            "Backend Developer", "Build APIs", "Engineering", "Remote", null,
            EmploymentType.FullTime, ExperienceLevel.Mid, WorkArrangement.Remote, null, Guid.NewGuid());

        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        return job.Id;
    }
}
