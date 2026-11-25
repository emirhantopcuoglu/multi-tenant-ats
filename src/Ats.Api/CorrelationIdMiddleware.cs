using System.Diagnostics;
using System.Text.RegularExpressions;
using Serilog.Context;

namespace Ats.Api;

// Reads or generates the correlation id, echoes it in the response, and adds it to the log context
// and the current trace span.
public sealed partial class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";

    // The client's id ends up in every log line of the request, so anything longer or odder is replaced.
    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex AcceptedId();

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();
        var correlationId = incoming is not null && AcceptedId().IsMatch(incoming)
            ? incoming
            : Guid.NewGuid().ToString("N");

        context.Response.Headers[CorrelationIdHeader] = correlationId;

        Activity.Current?.SetTag("app.correlation_id", correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
