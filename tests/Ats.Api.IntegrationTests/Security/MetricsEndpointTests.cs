using System.Net;
using Ats.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ats.Api.IntegrationTests.Security;

[Collection(ApiTestCollection.Name)]
public sealed class MetricsEndpointTests
{
    private readonly AtsApiFactory _factory;

    public MetricsEndpointTests(AtsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task should_not_serve_metrics_on_the_public_port()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task should_serve_metrics_on_the_metrics_port()
    {
        var metricsPort = _factory.Services.GetRequiredService<IConfiguration>().GetValue<int>("Metrics:Port");
        await using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddTransient<IStartupFilter>(_ => new LocalPortStartupFilter(metricsPort))));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
