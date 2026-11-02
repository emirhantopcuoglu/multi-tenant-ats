using System.Net;
using System.Net.Http.Json;
using Ats.Api.IntegrationTests.Infrastructure;
using Ats.Modules.Tenants.Domain;

namespace Ats.Api.IntegrationTests.Applications;

[Collection(ApiTestCollection.Name)]
public sealed class ApplicationAuthorizationTests
{
    private readonly AtsApiFactory _factory;

    public ApplicationAuthorizationTests(AtsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task should_forbid_hiring_manager_from_rejecting_an_application()
    {
        var client = _factory.CreateCompanyClient(Roles.HiringManager, Guid.NewGuid());

        var response = await client.PostAsJsonAsync(RejectUrl(Guid.NewGuid()), new { reason = "Not a fit." });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // Shows the 403 above comes from the policy, not a broken token.
    [Fact]
    public async Task should_let_recruiter_reach_the_reject_handler()
    {
        var client = _factory.CreateCompanyClient(Roles.Recruiter, Guid.NewGuid());

        var response = await client.PostAsJsonAsync(RejectUrl(Guid.NewGuid()), new { reason = "Not a fit." });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task should_forbid_read_only_user_from_rejecting_an_application()
    {
        var client = _factory.CreateCompanyClient(Roles.ReadOnly, Guid.NewGuid());

        var response = await client.PostAsJsonAsync(RejectUrl(Guid.NewGuid()), new { reason = "Not a fit." });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static string RejectUrl(Guid applicationId) => $"/api/v1/applications/{applicationId}/reject";
}
