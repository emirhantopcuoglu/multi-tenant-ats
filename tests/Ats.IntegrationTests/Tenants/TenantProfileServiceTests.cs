using Ats.IntegrationTests.Shared;
using Ats.Modules.Tenants.Application;
using Ats.Modules.Tenants.Domain;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Kernel;

namespace Ats.IntegrationTests.Tenants;

// Exercises the settings-side profile service against real Postgres: the tenant row is resolved
// from the caller's tenant context (no global filter on Tenant), values persist trimmed, and the
// boundary validation returns typed errors instead of letting the entity guard throw.
[Collection("Integration")]
public sealed class TenantProfileServiceTests
{
    private readonly PostgresContainerFixture _fixture;

    public TenantProfileServiceTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task should_update_the_profile_and_read_it_back()
    {
        // Arrange
        var tenantId = await SeedTenantAsync("Acme Inc", $"acme-{Guid.NewGuid():N}");

        // Act — values arrive untrimmed, as a form would send them
        var update = await UpdateAsync(tenantId, new UpdateTenantProfileRequest(
            "  We build rockets.  ", " https://acme.example.com ", " Istanbul, TR "));
        var read = await GetAsync(tenantId);

        // Assert — persisted trimmed, and the same values come back on a fresh read
        Assert.True(update.IsSuccess);
        Assert.True(read.IsSuccess);
        Assert.Equal("We build rockets.", read.Value.Description);
        Assert.Equal("https://acme.example.com", read.Value.Website);
        Assert.Equal("Istanbul, TR", read.Value.Location);
        Assert.Equal("Acme Inc", read.Value.CompanyName);
    }

    [Fact]
    public async Task should_clear_fields_when_the_update_sends_blank_values()
    {
        // Arrange — a profile that already has values
        var tenantId = await SeedTenantAsync("Acme Inc", $"acme-{Guid.NewGuid():N}");
        await UpdateAsync(tenantId, new UpdateTenantProfileRequest(
            "Old description", "https://old.example.com", "Old Town"));

        // Act — blank/whitespace input means "clear this field"
        var result = await UpdateAsync(tenantId, new UpdateTenantProfileRequest("", "   ", null));

        // Assert — cleared fields read back as null, not empty strings
        Assert.True(result.IsSuccess);
        var read = await GetAsync(tenantId);
        Assert.Null(read.Value.Description);
        Assert.Null(read.Value.Website);
        Assert.Null(read.Value.Location);
    }

    [Theory]
    [InlineData("acme.example.com")]          // relative
    [InlineData("ftp://acme.example.com")]    // wrong scheme
    [InlineData("not a url")]
    public async Task should_reject_a_website_that_is_not_an_absolute_http_url(string website)
    {
        // Arrange
        var tenantId = await SeedTenantAsync("Acme Inc", $"acme-{Guid.NewGuid():N}");

        // Act
        var result = await UpdateAsync(tenantId, new UpdateTenantProfileRequest(null, website, null));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(TenantProfileErrors.WebsiteNotAnAbsoluteHttpUrl.Code, result.Error.Code);
    }

    [Fact]
    public async Task should_reject_a_description_that_exceeds_the_length_cap()
    {
        // Arrange
        var tenantId = await SeedTenantAsync("Acme Inc", $"acme-{Guid.NewGuid():N}");
        var tooLong = new string('a', Tenant.DescriptionMaxLength + 1);

        // Act
        var result = await UpdateAsync(tenantId, new UpdateTenantProfileRequest(tooLong, null, null));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(TenantProfileErrors.DescriptionTooLong.Code, result.Error.Code);
    }

    [Fact]
    public async Task should_return_not_found_when_the_callers_tenant_does_not_exist()
    {
        // Act — a tenant id that has no row (e.g. deleted after the token was minted)
        var result = await GetAsync(Guid.NewGuid());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(TenantProfileErrors.TenantNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task should_update_only_the_callers_tenant()
    {
        // Arrange — two tenants; only Acme's admin updates its profile
        var acmeId = await SeedTenantAsync("Acme Inc", $"acme-{Guid.NewGuid():N}");
        var globexId = await SeedTenantAsync("Globex", $"globex-{Guid.NewGuid():N}");

        // Act
        await UpdateAsync(acmeId, new UpdateTenantProfileRequest("Acme only", null, null));

        // Assert — Globex is untouched
        var globex = await GetAsync(globexId);
        Assert.True(globex.IsSuccess);
        Assert.Null(globex.Value.Description);
    }

    private async Task<Result<TenantProfileDto>> GetAsync(Guid tenantId)
    {
        var tenant = new FixedTenant(tenantId);
        await using var db = new TenantsDbContext(
            PostgresContainerFixture.BuildTenantsOptions(_fixture.ConnectionString, tenant), tenant);

        return await new TenantProfileService(db, tenant).GetAsync();
    }

    private async Task<Result<TenantProfileDto>> UpdateAsync(
        Guid tenantId, UpdateTenantProfileRequest request)
    {
        var tenant = new FixedTenant(tenantId);
        await using var db = new TenantsDbContext(
            PostgresContainerFixture.BuildTenantsOptions(_fixture.ConnectionString, tenant), tenant);

        return await new TenantProfileService(db, tenant).UpdateAsync(request);
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
}
