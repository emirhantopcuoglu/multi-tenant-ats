using Ats.Api.IntegrationTests.Infrastructure;

namespace Ats.Api.IntegrationTests.Security;

// The id goes into every log line of the request, so only a short, plain one is taken from the client.
[Collection(ApiTestCollection.Name)]
public sealed class CorrelationIdTests
{
    private const string Header = "X-Correlation-ID";

    private readonly AtsApiFactory _factory;

    public CorrelationIdTests(AtsApiFactory factory) => _factory = factory;

    [Fact]
    public async Task a_plain_id_from_the_client_should_be_kept()
    {
        var id = Guid.NewGuid().ToString();

        Assert.Equal(id, await SendAsync(id));
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(65)]
    public async Task an_oversized_id_should_be_replaced(int length)
    {
        var sent = new string('a', length);

        var echoed = await SendAsync(sent);

        Assert.NotEqual(sent, echoed);
        Assert.Matches("^[0-9a-f]{32}$", echoed);
    }

    [Fact]
    public async Task an_id_with_other_characters_should_be_replaced()
    {
        var echoed = await SendAsync("abc {Message} <x>");

        Assert.Matches("^[0-9a-f]{32}$", echoed);
    }

    private async Task<string> SendAsync(string correlationId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation(Header, correlationId);
        using var response = await _factory.CreateClient().SendAsync(request);
        return Assert.Single(response.Headers.GetValues(Header));
    }
}
