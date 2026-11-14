using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Ats.Api.IntegrationTests.Infrastructure;
using Ats.Modules.Tenants.Domain;

namespace Ats.Api.IntegrationTests.Security;

[Collection(ApiTestCollection.Name)]
public sealed class SecurityHeadersTests
{
    private const string ApiPolicy = "default-src 'none'; frame-ancestors 'none'";

    private readonly AtsApiFactory _factory;

    public SecurityHeadersTests(AtsApiFactory factory) => _factory = factory;

    [Fact]
    public async Task an_api_response_should_forbid_scripts_framing_and_sniffing()
    {
        var response = await _factory.CreateClient().GetAsync("/api/v1/jobs");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        AssertApiHeaders(response);
    }

    // The exception handler clears the response headers before it writes the problem.
    [Fact]
    public async Task an_error_from_the_exception_handler_should_keep_them()
    {
        var client = _factory.CreateCompanyClient(Roles.Admin, Guid.NewGuid());
        var created = await client.PostAsJsonAsync("/api/v1/jobs", JobBody("Backend Developer", version: 0));
        created.EnsureSuccessStatusCode();
        var jobUrl = created.Headers.Location!.PathAndQuery;
        var version = (await client.GetFromJsonAsync<JsonElement>(jobUrl)).GetProperty("version").GetUInt32();
        (await client.PutAsJsonAsync(jobUrl, JobBody("Senior Backend Developer", version))).EnsureSuccessStatusCode();

        var stale = await client.PutAsJsonAsync(jobUrl, JobBody("Backend Engineer", version));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        AssertApiHeaders(stale);
    }

    // The dashboard is a page with scripts; the API policy would leave it blank.
    [Fact]
    public async Task the_hangfire_dashboard_should_not_get_the_api_policy()
    {
        var response = await _factory.CreateClient().GetAsync("/hangfire");

        Assert.False(response.Headers.Contains("Content-Security-Policy"));
    }

    private static void AssertApiHeaders(HttpResponseMessage response)
    {
        Assert.Equal(ApiPolicy, Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    private static object JobBody(string title, uint version) => new
    {
        title,
        description = "Build APIs",
        department = "Engineering",
        city = "Istanbul",
        country = "Turkey",
        employmentType = "FullTime",
        experienceLevel = "Mid",
        workArrangement = "Remote",
        salaryMin = (decimal?)null,
        salaryMax = (decimal?)null,
        salaryCurrency = (string?)null,
        version,
    };
}
