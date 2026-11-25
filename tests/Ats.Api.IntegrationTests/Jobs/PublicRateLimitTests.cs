using System.Net;
using Ats.Api.IntegrationTests.Infrastructure;
using Ats.Modules.Tenants.Domain;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ats.Api.IntegrationTests.Jobs;

[Collection(ApiTestCollection.Name)]
public sealed class PublicRateLimitTests
{
    private readonly AtsApiFactory _factory;

    public PublicRateLimitTests(AtsApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/public/jobs?search=engineer")]
    [InlineData("/public/companies")]
    [InlineData("/{0}/jobs")]
    public async Task public_pages_should_be_limited_per_ip(string urlFormat)
    {
        var url = string.Format(urlFormat, await SeedCompanyAsync());
        var limit = _factory.Services.GetRequiredService<IConfiguration>()
            .GetSection(RateLimitingOptions.SectionName).Get<RateLimitingOptions>()!.PublicReadPermitLimit;
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(ExternalClientIpStartupFilter.ClientIpHeader, ExternalClientIpStartupFilter.NextClientIp());

        for (var i = 0; i < limit; i++)
        {
            using var allowed = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }

        using var refused = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
    }

    private async Task<string> SeedCompanyAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
        var company = Tenant.Create("Acme", $"acme-{Guid.NewGuid():N}"[..20]);
        db.Tenants.Add(company);
        await db.SaveChangesAsync();
        return company.Slug;
    }
}
