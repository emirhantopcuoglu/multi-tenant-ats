using System.Text.Json;
using Ats.Modules.Jobs.Domain;
using Ats.Modules.Jobs.Infrastructure;
using Ats.Shared.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Ats.IntegrationTests.Shared;

[Collection("Integration")]
public sealed class AuditLogInterceptorTests
{
    private readonly PostgresContainerFixture _fixture;
    private readonly FixedTenant _tenant = new(Guid.NewGuid());
    private readonly FixedCurrentUser _user = new(Guid.NewGuid(), "recruiter@example.test");

    public AuditLogInterceptorTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task a_saved_change_should_leave_an_audit_row_in_the_same_database()
    {
        var job = await CreateJobAsync();

        var entry = Assert.Single(await AuditRowsForAsync(job.Id));
        Assert.Equal("Created", entry.Action);
        Assert.Equal("Job", entry.ResourceType);
        Assert.Equal(_tenant.TenantId, entry.TenantId);
        Assert.Equal(_user.UserId, entry.ActorUserId);
        Assert.Null(entry.Before);
        using var after = JsonDocument.Parse(entry.After!);
        Assert.Equal(job.Title, after.RootElement.GetProperty("Title").GetString());
    }

    [Fact]
    public async Task an_update_should_record_only_the_changed_fields()
    {
        var job = await CreateJobAsync();
        await using (var db = NewDb())
        {
            (await db.Jobs.SingleAsync(j => j.Id == job.Id)).Publish();
            await db.SaveChangesAsync();
        }

        var update = Assert.Single(await AuditRowsForAsync(job.Id), e => e.Action == "Updated");
        using var before = JsonDocument.Parse(update.Before!);
        using var after = JsonDocument.Parse(update.After!);
        Assert.Equal("Draft", before.RootElement.GetProperty("Status").GetString());
        Assert.Equal("Published", after.RootElement.GetProperty("Status").GetString());
        Assert.False(after.RootElement.TryGetProperty("Title", out _));
    }

    // EF tracks an owned value such as SalaryRange as entries of its own, and the job stays Unchanged.
    [Fact]
    public async Task an_added_owned_value_should_be_recorded()
    {
        var job = await CreateJobAsync();
        await ChangeSalaryAsync(job.Id, new SalaryRange(90_000, 120_000, "EUR"));

        var update = Assert.Single(await AuditRowsForAsync(job.Id), e => e.Action == "Updated");
        using var after = JsonDocument.Parse(update.After!);
        Assert.Equal(120_000, after.RootElement.GetProperty("SalaryRange.Max").GetDecimal());
    }

    // The new value is tracked as Added and the old one as Deleted; together they are one change.
    [Fact]
    public async Task a_replaced_owned_value_should_record_only_what_changed()
    {
        var job = await CreateJobAsync(new SalaryRange(90_000, 120_000, "EUR"));
        await ChangeSalaryAsync(job.Id, new SalaryRange(90_000, 130_000, "EUR"));

        var update = Assert.Single(await AuditRowsForAsync(job.Id), e => e.Action == "Updated");
        using var before = JsonDocument.Parse(update.Before!);
        using var after = JsonDocument.Parse(update.After!);
        Assert.Equal(120_000, before.RootElement.GetProperty("SalaryRange.Max").GetDecimal());
        Assert.Equal(130_000, after.RootElement.GetProperty("SalaryRange.Max").GetDecimal());
        Assert.False(after.RootElement.TryGetProperty("SalaryRange.Currency", out _));
    }

    [Fact]
    public async Task a_deleted_record_should_be_recorded_as_deleted()
    {
        var job = await CreateJobAsync();
        await using (var db = NewDb())
        {
            db.Jobs.Remove(await db.Jobs.SingleAsync(j => j.Id == job.Id));
            await db.SaveChangesAsync();
        }

        Assert.Contains(await AuditRowsForAsync(job.Id), e => e.Action == "Deleted");
    }

    // Two people edit the same job. The second save loses on xmin and rolls back, so its audit row
    // must not exist either.
    [Fact]
    public async Task a_failed_save_should_leave_no_audit_row()
    {
        var job = await CreateJobAsync();
        await using var first = NewDb();
        await using var second = NewDb();
        var firstCopy = await first.Jobs.SingleAsync(j => j.Id == job.Id);
        var secondCopy = await second.Jobs.SingleAsync(j => j.Id == job.Id);

        firstCopy.Publish();
        await first.SaveChangesAsync();
        secondCopy.Archive();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());

        var rows = await AuditRowsForAsync(job.Id);
        Assert.Equal(["Created", "Updated"], rows.Select(e => e.Action));
        Assert.DoesNotContain(rows, e => e.After?.Contains("Archived") == true);
    }

    // The header comes from the client, and the audit row now shares the change's transaction.
    [Fact]
    public async Task a_very_long_user_agent_should_not_fail_the_save()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers.UserAgent = new string('a', 2000);

        var job = await CreateJobAsync(new HttpContextAccessor { HttpContext = httpContext });

        var entry = Assert.Single(await AuditRowsForAsync(job.Id));
        Assert.Equal(AuditLogEntry.UserAgentMaxLength, entry.UserAgent!.Length);
    }

    private Task<Job> CreateJobAsync(IHttpContextAccessor httpContextAccessor) =>
        CreateJobAsync(salaryRange: null, httpContextAccessor);

    private async Task<Job> CreateJobAsync(
        SalaryRange? salaryRange = null, IHttpContextAccessor? httpContextAccessor = null)
    {
        var job = Job.Create(
            "Backend Developer", "A description long enough to publish.", "Engineering", "Remote", null,
            EmploymentType.FullTime, ExperienceLevel.Senior, WorkArrangement.Remote, salaryRange, Guid.NewGuid());
        await using var db = NewDb(httpContextAccessor);
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        return job;
    }

    private async Task ChangeSalaryAsync(Guid jobId, SalaryRange salaryRange)
    {
        await using var db = NewDb();
        var job = await db.Jobs.SingleAsync(j => j.Id == jobId);
        job.UpdateDetails(
            job.Title, job.Description, job.Department, job.City, job.Country,
            job.EmploymentType, job.ExperienceLevel, job.WorkArrangement, salaryRange);
        await db.SaveChangesAsync();
    }

    private async Task<List<AuditLogEntry>> AuditRowsForAsync(Guid jobId)
    {
        await using var db = NewDb();
        var resourceId = jobId.ToString();
        return await db.Set<AuditLogEntry>()
            .Where(e => e.ResourceId == resourceId)
            .OrderBy(e => e.OccurredAtUtc)
            .ToListAsync();
    }

    // Same interceptor order as Program.cs: tenant, auditable fields, then the audit log.
    private JobsDbContext NewDb(IHttpContextAccessor? httpContextAccessor = null)
    {
        var interceptor = new AuditLogInterceptor(_tenant, _user, httpContextAccessor ?? new HttpContextAccessor());
        var options = new DbContextOptionsBuilder<JobsDbContext>(
                PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, _tenant))
            .AddInterceptors(interceptor)
            .Options;
        return new JobsDbContext(options, _tenant);
    }
}
