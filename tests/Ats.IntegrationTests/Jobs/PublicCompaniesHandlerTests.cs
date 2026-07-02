using Ats.IntegrationTests.Shared;
using Ats.Modules.Jobs.Application;
using Ats.Modules.Jobs.Application.Jobs;
using Ats.Modules.Jobs.Domain;
using Ats.Modules.Jobs.Infrastructure;
using Ats.Modules.Tenants.Domain;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.IntegrationTests.Jobs;

// The public companies directory spans all tenants (IgnoreQueryFilters), so — like the job feed — it
// sees rows other tests leave in the shared container. These tests start from an empty Jobs + Tenants
// state so the counts and ordering are deterministic.
[Collection("Integration")]
public sealed class PublicCompaniesHandlerTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;

    public PublicCompaniesHandlerTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var tenant = new FixedTenant(null);
        await using var jobsDb = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
        await jobsDb.Database.ExecuteSqlRawAsync("DELETE FROM jobs.\"Jobs\"");

        await using var tenantsDb = new TenantsDbContext(
            PostgresContainerFixture.BuildTenantsOptions(_fixture.ConnectionString, tenant), tenant);
        await tenantsDb.Database.ExecuteSqlRawAsync("DELETE FROM tenants.\"Tenants\"");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task should_list_only_companies_with_published_jobs_ordered_by_open_count()
    {
        // Arrange — Acme has two published roles, Globex one; Umbrella exists but only as a draft, so
        // it is not "hiring" and must not appear in the directory.
        var acmeId = await SeedTenantAsync("Acme Inc", "acme");
        var globexId = await SeedTenantAsync("Globex", "globex");
        var umbrellaId = await SeedTenantAsync("Umbrella", "umbrella");

        await SeedPublishedJobAsync(acmeId, "Backend Engineer");
        await SeedPublishedJobAsync(acmeId, "Frontend Engineer");
        await SeedPublishedJobAsync(globexId, "Product Designer");
        await SeedDraftJobAsync(umbrellaId, "Unannounced Role");

        // Act
        var result = await ListAsync(new ListPublicCompaniesQuery());

        // Assert — two hiring companies, most-hiring first, each with the right open-role count
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);

        Assert.Equal("Acme Inc", result.Value.Items[0].CompanyName);
        Assert.Equal("acme", result.Value.Items[0].Slug);
        Assert.Equal(2, result.Value.Items[0].OpenJobCount);

        Assert.Equal("Globex", result.Value.Items[1].CompanyName);
        Assert.Equal(1, result.Value.Items[1].OpenJobCount);

        Assert.DoesNotContain(result.Value.Items, c => c.Slug == "umbrella");
    }

    [Fact]
    public async Task should_filter_the_directory_by_company_name_search()
    {
        // Arrange
        var acmeId = await SeedTenantAsync("Acme Inc", "acme");
        var globexId = await SeedTenantAsync("Globex", "globex");
        await SeedPublishedJobAsync(acmeId, "Backend Engineer");
        await SeedPublishedJobAsync(globexId, "Product Designer");

        // Act — search matches one company name
        var result = await ListAsync(new ListPublicCompaniesQuery(Search: "globex"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("Globex", result.Value.Items[0].CompanyName);
    }

    [Fact]
    public async Task should_return_company_profile_by_slug_with_open_job_count()
    {
        // Arrange
        var acmeId = await SeedTenantAsync("Acme Inc", "acme");
        await SeedPublishedJobAsync(acmeId, "Backend Engineer");
        await SeedDraftJobAsync(acmeId, "Secret Role");

        // Act
        var result = await GetBySlugAsync("acme");

        // Assert — the profile counts only published roles, not the draft
        Assert.True(result.IsSuccess);
        Assert.Equal("Acme Inc", result.Value.CompanyName);
        Assert.Equal("acme", result.Value.Slug);
        Assert.Equal(1, result.Value.OpenJobCount);
    }

    [Fact]
    public async Task should_return_not_found_for_an_unknown_company_slug()
    {
        // Act — no tenant owns this slug
        var result = await GetBySlugAsync("nonexistent");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(CompanyErrors.NotFound.Code, result.Error.Code);
    }

    private async Task<Result<PagedResult<PublicCompanyDto>>> ListAsync(ListPublicCompaniesQuery query)
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        await using var jobsDb = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
        await using var tenantsDb = new TenantsDbContext(
            PostgresContainerFixture.BuildTenantsOptions(_fixture.ConnectionString, tenant), tenant);

        var directory = new TenantDirectory(tenantsDb);
        return await new ListPublicCompaniesHandler(jobsDb, directory).Handle(query, CancellationToken.None);
    }

    private async Task<Result<PublicCompanyDto>> GetBySlugAsync(string slug)
    {
        var tenant = new FixedTenant(Guid.NewGuid());
        await using var jobsDb = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
        await using var tenantsDb = new TenantsDbContext(
            PostgresContainerFixture.BuildTenantsOptions(_fixture.ConnectionString, tenant), tenant);

        var directory = new TenantDirectory(tenantsDb);
        return await new GetPublicCompanyBySlugHandler(jobsDb, directory)
            .Handle(new GetPublicCompanyBySlugQuery(slug), CancellationToken.None);
    }

    private async Task<Guid> SeedTenantAsync(string name, string slug)
    {
        var tenant = new FixedTenant(null);
        await using var db = new TenantsDbContext(
            PostgresContainerFixture.BuildTenantsOptions(_fixture.ConnectionString, tenant), tenant);

        var company = Tenant.Create(name, slug);
        db.Tenants.Add(company);
        await db.SaveChangesAsync();
        return company.Id;
    }

    private Task SeedPublishedJobAsync(Guid tenantId, string title) => SeedJobAsync(tenantId, title, publish: true);

    private Task SeedDraftJobAsync(Guid tenantId, string title) => SeedJobAsync(tenantId, title, publish: false);

    private async Task SeedJobAsync(Guid tenantId, string title, bool publish)
    {
        var tenant = new FixedTenant(tenantId);
        await using var db = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);

        var job = Job.Create(
            title, "A role", "Engineering", "Remote",
            EmploymentType.FullTime, ExperienceLevel.Mid, salaryRange: null, Guid.NewGuid());
        if (publish)
            job.Publish();

        db.Jobs.Add(job);
        await db.SaveChangesAsync();
    }
}
