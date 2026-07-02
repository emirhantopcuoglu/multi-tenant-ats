using System.Diagnostics;
using Serilog.Context;

namespace Ats.Api;

// Assigns a correlation ID to every request (read from the incoming header or generated fresh).
// Echoes the ID in the response header so callers can correlate requests across systems.
// Pushes it into Serilog's LogContext (log lines) and the current OTel Activity (Jaeger span)
// so the same ID is queryable in both Seq and Jaeger without any extra instrumentation.
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

        // Tag the OTel span so the correlation ID is searchable in Jaeger.
        // Activity.Current is set by the AspNetCore instrumentation before our middleware runs.
        Activity.Current?.SetTag("app.correlation_id", correlationId);

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
