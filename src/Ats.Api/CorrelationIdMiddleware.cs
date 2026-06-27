using Serilog.Context;

namespace Ats.Api;

// Assigns a correlation ID to every request (read from the incoming header or generated fresh).
// Echoes the ID in the response header so callers can correlate requests across systems.
// Pushes it into Serilog's LogContext so every log line emitted during the request carries it —
// without needing to pass it through every method call.
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

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
