using System.Diagnostics;
using Serilog.Context;

namespace Ats.Api;

// Reads or generates the correlation id, echoes it in the response, and adds it to the log context
// and the current trace span.
public sealed class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");

        context.Response.Headers[CorrelationIdHeader] = correlationId;

        Activity.Current?.SetTag("app.correlation_id", correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
