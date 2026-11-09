namespace Ats.Api;

// OWASP's headers for a JSON API. The CSP is only for /api: the Scalar docs and the Hangfire
// dashboard are pages that need their scripts.
public sealed class ApiSecurityHeadersMiddleware
{
    private const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    private readonly RequestDelegate _next;

    public ApiSecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var isApi = context.Request.Path.StartsWithSegments("/api");

        // OnStarting, so an error response still gets them after the exception handler clears headers.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.XContentTypeOptions = "nosniff";
            if (isApi)
                context.Response.Headers.ContentSecurityPolicy = ApiContentSecurityPolicy;

            return Task.CompletedTask;
        });

        return _next(context);
    }
}
