using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Ats.UnitTests.Shared;

internal sealed class StubHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

    // disposeHandler: false, the parser disposes the client it is handed, and the test still
    // needs to read what the handler saw afterwards.
    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
}

// Stands in for the provider: answers with Status (a fixed parse result on 200) and keeps what it was sent.
internal sealed class FakeLlmHandler : HttpMessageHandler
{
    private const string ParseResult = """{"skills":[],"job_fit_rating":"Weak"}""";

    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    public int Calls { get; private set; }

    public string LastUserMessage { get; private set; } = "";

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        LastUserMessage = body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!;

        if (Status != HttpStatusCode.OK)
            return new HttpResponseMessage(Status);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { choices = new[] { new { message = new { content = ParseResult } } } })
        };
    }
}
