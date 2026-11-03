using System.Net;
using Ats.Api.IntegrationTests.Infrastructure;
using Ats.Modules.Tenants.Domain;

namespace Ats.Api.IntegrationTests.Security;

[Collection(ApiTestCollection.Name)]
public sealed class CorsTests
{
    private const string ProtectedUrl = "/api/v1/jobs";

    private readonly AtsApiFactory _factory;

    public CorsTests(AtsApiFactory factory)
    {
        _factory = factory;
    }

    // The browser sends preflight without a token, so CORS has to answer it before tenant resolution and auth.
    [Fact]
    public async Task should_answer_a_preflight_from_the_spa_without_a_token()
    {
        using var response = await _factory.CreateClient().SendAsync(Preflight(AtsApiFactory.SpaOrigin));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(AtsApiFactory.SpaOrigin, AllowedOrigin(response));
        Assert.Equal("true", Header(response, "Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task should_not_allow_an_unknown_origin()
    {
        using var response = await _factory.CreateClient().SendAsync(Preflight("https://evil.test"));

        Assert.Null(AllowedOrigin(response));
    }

    // Without the header the browser hides the 401, and the SPA can't refresh its token.
    [Fact]
    public async Task should_let_the_spa_read_a_401()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ProtectedUrl);
        request.Headers.Add("Origin", AtsApiFactory.SpaOrigin);

        using var response = await _factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(AtsApiFactory.SpaOrigin, AllowedOrigin(response));
    }

    // The SPA reads Retry-After to tell the user when to try again.
    [Fact]
    public async Task should_expose_retry_after_to_the_spa()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ProtectedUrl);
        request.Headers.Add("Origin", AtsApiFactory.SpaOrigin);

        using var response = await _factory.CreateCompanyClient(Roles.Admin, Guid.NewGuid()).SendAsync(request);

        Assert.Contains("Retry-After", Header(response, "Access-Control-Expose-Headers") ?? "");
    }

    private static HttpRequestMessage Preflight(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, ProtectedUrl);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "authorization");
        return request;
    }

    private static string? AllowedOrigin(HttpResponseMessage response) =>
        Header(response, "Access-Control-Allow-Origin");

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? string.Join(",", values) : null;
}
