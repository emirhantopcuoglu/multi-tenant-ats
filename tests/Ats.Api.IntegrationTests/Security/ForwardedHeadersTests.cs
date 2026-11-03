using System.Net;
using System.Net.Http.Json;
using Ats.Api.IntegrationTests.Infrastructure;

namespace Ats.Api.IntegrationTests.Security;

[Collection(ApiTestCollection.Name)]
public sealed class ForwardedHeadersTests
{
    // Matches RateLimiting:PerIpPermitLimit in appsettings.json.
    private const int PerIpPermitLimit = 5;

    private readonly AtsApiFactory _factory;

    public ForwardedHeadersTests(AtsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task should_not_open_hangfire_dashboard_for_spoofed_localhost()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For", "127.0.0.1");

        var response = await client.GetAsync("/hangfire");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task should_rate_limit_login_even_when_forwarded_for_changes()
    {
        var client = _factory.CreateClient();
        for (var attempt = 1; attempt <= PerIpPermitLimit; attempt++)
            await SendLoginAsync(client, $"203.0.113.{attempt}");

        var response = await SendLoginAsync(client, "203.0.113.99");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    private static Task<HttpResponseMessage> SendLoginAsync(HttpClient client, string forwardedFor)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email = "nobody@acme.test", password = "wrong" })
        };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        return client.SendAsync(request);
    }
}
