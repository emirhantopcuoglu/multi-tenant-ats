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

// Stands in for the provider: answers with Status and, on 200, with the next of Replies (a default
// parse result once they run out), and keeps every user message it was sent.
internal sealed class FakeLlmHandler : HttpMessageHandler
{
    private const string DefaultReply = """{"skills":[],"job_fit_rating":"Weak"}""";

    public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

    public Queue<string> Replies { get; } = new();

    public List<string> UserMessages { get; } = [];

    public int Calls => UserMessages.Count;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        UserMessages.Add(body.RootElement.GetProperty("messages")[1].GetProperty("content").GetString()!);

        if (Status != HttpStatusCode.OK)
            return new HttpResponseMessage(Status);

        var reply = Replies.Count > 0 ? Replies.Dequeue() : DefaultReply;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { choices = new[] { new { message = new { content = reply } } } })
        };
    }
}
