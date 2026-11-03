using Ats.IntegrationTests.Shared;
using Ats.Modules.Tenants.Application;
using Ats.Modules.Tenants.Domain;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.EntityFrameworkCore;

namespace Ats.IntegrationTests.Tenants;

// Against real Postgres: the tenant comes from the context, values are trimmed, and bad input
// returns typed errors.
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

        // Act: values arrive untrimmed, as a form would send them
        var update = await UpdateAsync(tenantId, "  We build rockets.  ", " https://acme.example.com ", " Istanbul, TR ");
        var read = await GetAsync(tenantId);

        // Assert: persisted trimmed, and the same values come back on a fresh read
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
        // Arrange: a profile that already has values
        var tenantId = await SeedTenantAsync("Acme Inc", $"acme-{Guid.NewGuid():N}");
        await UpdateAsync(tenantId, "Old description", "https://old.example.com", "Old Town");

        // Act: blank/whitespace input means "clear this field"
        var result = await UpdateAsync(tenantId, "", "   ", null);

        // Assert: cleared fields read back as null, not empty strings
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
        var result = await UpdateAsync(tenantId, null, website, null);

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
        var result = await UpdateAsync(tenantId, tooLong, null, null);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(TenantProfileErrors.DescriptionTooLong.Code, result.Error.Code);
    }

    [Fact]
    public async Task should_return_not_found_when_the_callers_tenant_does_not_exist()
    {
        // Act: a tenant id that has no row (e.g. deleted after the token was minted)
        var result = await GetAsync(Guid.NewGuid());

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(TenantProfileErrors.TenantNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task should_update_only_the_callers_tenant()
    {
        // Arrange: two tenants; only Acme's admin updates its profile
        var acmeId = await SeedTenantAsync("Acme Inc", $"acme-{Guid.NewGuid():N}");
        var globexId = await SeedTenantAsync("Globex", $"globex-{Guid.NewGuid():N}");

        // Act
        await UpdateAsync(acmeId, "Acme only", null, null);

        // Assert: Globex is untouched
        var globex = await GetAsync(globexId);
        Assert.True(globex.IsSuccess);
        Assert.Null(globex.Value.Description);
    }

    [Fact]
    public async Task should_refuse_a_save_from_a_form_loaded_before_someone_else_saved()
    {
        var tenantId = await SeedTenantAsync("Acme Inc", $"acme-{Guid.NewGuid():N}");
        var loadedByBoth = await GetAsync(tenantId);

        var first = await UpdateAsync(
            tenantId, new UpdateTenantProfileRequest("We build rockets.", null, null, loadedByBoth.Value.Version));
        var second = () => UpdateAsync(
            tenantId, new UpdateTenantProfileRequest(null, null, "Istanbul", loadedByBoth.Value.Version));

        Assert.True(first.IsSuccess);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(second);
        Assert.Equal("We build rockets.", (await GetAsync(tenantId)).Value.Description);
    }

    // The form keeps the version from the save's response, so the next save must accept it.
    [Fact]
    public async Task should_return_a_version_that_the_next_save_accepts()
    {
        var tenantId = await SeedTenantAsync("Acme Inc", $"acme-{Guid.NewGuid():N}");
        var loaded = await GetAsync(tenantId);

        var first = await UpdateAsync(
            tenantId, new UpdateTenantProfileRequest("We build rockets.", null, null, loaded.Value.Version));
        var second = await UpdateAsync(
            tenantId, new UpdateTenantProfileRequest("We build rockets.", null, "Istanbul", first.Value.Version));

        Assert.True(second.IsSuccess);
        Assert.Equal("Istanbul", (await GetAsync(tenantId)).Value.Location);
    }

    private async Task<Result<TenantProfileDto>> GetAsync(Guid tenantId)
    {
        var tenant = new FixedTenant(tenantId);
        await using var db = new TenantsDbContext(
            PostgresContainerFixture.BuildTenantsOptions(_fixture.ConnectionString, tenant), tenant);

        return await new TenantProfileService(db, tenant).GetAsync();
    }

    // Saves from a fresh load, the way a form opened just now would.
    private async Task<Result<TenantProfileDto>> UpdateAsync(
        Guid tenantId, string? description, string? website, string? location)
    {
        var loaded = await GetAsync(tenantId);
        return await UpdateAsync(
            tenantId, new UpdateTenantProfileRequest(description, website, location, loaded.Value.Version));
    }

    private async Task<Result<TenantProfileDto>> UpdateAsync(Guid tenantId, UpdateTenantProfileRequest request)
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
