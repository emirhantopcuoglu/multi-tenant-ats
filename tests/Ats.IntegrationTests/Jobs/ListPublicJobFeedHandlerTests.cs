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

// The cross-tenant public feed is the one query that deliberately ignores the tenant filter, so it
// can see rows every other test leaves behind in the shared container. These tests therefore start
// from an empty Jobs + Tenants state (the same DELETE approach ListTenantUsersTests uses) so the
// assertions can count exact rows.
[Collection("Integration")]
public sealed class ListPublicJobFeedHandlerTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;

    public ListPublicJobFeedHandlerTests(PostgresContainerFixture fixture)
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
    public async Task should_list_published_jobs_across_tenants_with_company_and_exclude_drafts()
    {
        // Arrange — two separate companies, each with a published job, plus a draft that must not leak
        var acmeId = await SeedTenantAsync("Acme Inc", "acme");
        var globexId = await SeedTenantAsync("Globex", "globex");

        await SeedPublishedJobAsync(acmeId, "Backend Engineer");
        await SeedPublishedJobAsync(globexId, "Product Designer");
        await SeedDraftJobAsync(acmeId, "Unannounced Role");

        // Act — the feed with no search term
        var result = await HandleAsync(new ListPublicJobFeedQuery());

        // Assert — both companies' published jobs appear, the draft does not, and each item is
        // stamped with the right company name + slug resolved through the Tenants port
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);

        var backend = result.Value.Items.Single(i => i.Title == "Backend Engineer");
        Assert.Equal("Acme Inc", backend.CompanyName);
        Assert.Equal("acme", backend.CompanySlug);

        var designer = result.Value.Items.Single(i => i.Title == "Product Designer");
        Assert.Equal("Globex", designer.CompanyName);
        Assert.Equal("globex", designer.CompanySlug);

        Assert.DoesNotContain(result.Value.Items, i => i.Title == "Unannounced Role");
    }

    [Fact]
    public async Task should_match_search_on_company_name()
    {
        // Arrange — the search term matches a company name, not any job title
        var acmeId = await SeedTenantAsync("Acme Inc", "acme");
        var globexId = await SeedTenantAsync("Globex", "globex");
        await SeedPublishedJobAsync(acmeId, "Backend Engineer");
        await SeedPublishedJobAsync(globexId, "Product Designer");

        // Act — "globex" appears in no title, only in the company name
        var result = await HandleAsync(new ListPublicJobFeedQuery(Search: "globex"));

        // Assert — the company-name match still returns that company's job
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("Product Designer", result.Value.Items[0].Title);
    }

    [Fact]
    public async Task should_match_search_on_job_title_across_tenants()
    {
        // Arrange — a shared title fragment ("engineer") spanning two companies
        var acmeId = await SeedTenantAsync("Acme Inc", "acme");
        var globexId = await SeedTenantAsync("Globex", "globex");
        await SeedPublishedJobAsync(acmeId, "Backend Engineer");
        await SeedPublishedJobAsync(globexId, "Frontend Engineer");
        await SeedPublishedJobAsync(globexId, "Product Designer");

        // Act
        var result = await HandleAsync(new ListPublicJobFeedQuery(Search: "engineer"));

        // Assert — both engineering roles match, the designer does not
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.All(result.Value.Items, i => Assert.Contains("Engineer", i.Title));
    }

    [Fact]
    public async Task should_filter_by_employment_type()
    {
        // Arrange — same company, different employment types
        var acmeId = await SeedTenantAsync("Acme Inc", "acme");
        await SeedPublishedJobAsync(acmeId, "Backend Engineer", employmentType: EmploymentType.FullTime);
        await SeedPublishedJobAsync(acmeId, "Support Intern", employmentType: EmploymentType.Internship);

        // Act — filter value arrives lowercase, as a query string would send it
        var result = await HandleAsync(new ListPublicJobFeedQuery(EmploymentType: "internship"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("Support Intern", result.Value.Items[0].Title);
    }

    [Fact]
    public async Task should_filter_by_experience_level()
    {
        // Arrange
        var acmeId = await SeedTenantAsync("Acme Inc", "acme");
        await SeedPublishedJobAsync(acmeId, "Junior Developer", experienceLevel: ExperienceLevel.Junior);
        await SeedPublishedJobAsync(acmeId, "Staff Engineer", experienceLevel: ExperienceLevel.Senior);

        // Act
        var result = await HandleAsync(new ListPublicJobFeedQuery(ExperienceLevel: "Senior"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("Staff Engineer", result.Value.Items[0].Title);
    }

    [Fact]
    public async Task should_filter_by_location_fragment()
    {
        // Arrange — locations are free text, so the filter must match substrings case-insensitively
        var acmeId = await SeedTenantAsync("Acme Inc", "acme");
        await SeedPublishedJobAsync(acmeId, "Office Manager", location: "Istanbul, TR");
        await SeedPublishedJobAsync(acmeId, "Backend Engineer", location: "Remote (EU)");

        // Act
        var result = await HandleAsync(new ListPublicJobFeedQuery(Location: "istanbul"));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("Office Manager", result.Value.Items[0].Title);
    }

    [Fact]
    public async Task should_ignore_unparseable_filter_values()
    {
        // Arrange — a hand-edited public URL with junk filters must degrade to the unfiltered list.
        // "99" specifically covers Enum.TryParse's numeric quirk (it parses into an undefined value).
        var acmeId = await SeedTenantAsync("Acme Inc", "acme");
        await SeedPublishedJobAsync(acmeId, "Backend Engineer");
        await SeedPublishedJobAsync(acmeId, "Product Designer");

        // Act
        var result = await HandleAsync(new ListPublicJobFeedQuery(
            EmploymentType: "not-a-type", ExperienceLevel: "99"));

        // Assert — both filters are ignored, nothing is silently hidden
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);
    }

    private async Task<Result<PagedResult<PublicJobFeedItemDto>>> HandleAsync(ListPublicJobFeedQuery query)
    {
        // The handler ignores the ambient tenant (IgnoreQueryFilters), so any tenant on the context is
        // fine; the port reads companies from a real TenantsDbContext.
        var tenant = new FixedTenant(Guid.NewGuid());
        await using var jobsDb = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);
        await using var tenantsDb = new TenantsDbContext(
            PostgresContainerFixture.BuildTenantsOptions(_fixture.ConnectionString, tenant), tenant);

        var directory = new TenantDirectory(tenantsDb);
        return await new ListPublicJobFeedHandler(jobsDb, directory).Handle(query, CancellationToken.None);
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

    private Task SeedPublishedJobAsync(
        Guid tenantId, string title,
        EmploymentType employmentType = EmploymentType.FullTime,
        ExperienceLevel experienceLevel = ExperienceLevel.Mid,
        string location = "Remote")
        => SeedJobAsync(tenantId, title, publish: true, employmentType, experienceLevel, location);

    private Task SeedDraftJobAsync(Guid tenantId, string title)
        => SeedJobAsync(tenantId, title, publish: false, EmploymentType.FullTime, ExperienceLevel.Mid, "Remote");

    private async Task SeedJobAsync(
        Guid tenantId, string title, bool publish,
        EmploymentType employmentType, ExperienceLevel experienceLevel, string location)
    {
        // The save-changes interceptor stamps TenantId from the ambient tenant, so seeding under
        // FixedTenant(tenantId) ties the job to that company.
        var tenant = new FixedTenant(tenantId);
        await using var db = new JobsDbContext(
            PostgresContainerFixture.BuildJobsOptions(_fixture.ConnectionString, tenant), tenant);

        var job = Job.Create(
            title, "A role", "Engineering", location,
            employmentType, experienceLevel, salaryRange: null, Guid.NewGuid());
        if (publish)
            job.Publish();

        db.Jobs.Add(job);
        await db.SaveChangesAsync();
    }
}
