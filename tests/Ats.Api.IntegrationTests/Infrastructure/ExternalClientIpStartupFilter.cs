using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Ats.Api.IntegrationTests.Infrastructure;

// TestServer leaves RemoteIpAddress null, and ForwardedHeaders trusts any header then. Kestrel always sets it.
public sealed class ExternalClientIpStartupFilter : IStartupFilter
{
    // Lets a test that sends many requests give each its own IP, so they don't share the per-IP bucket.
    public const string ClientIpHeader = "X-Test-Client-Ip";

    private static readonly IPAddress ExternalClientIp = IPAddress.Parse("198.51.100.10");

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            context.Connection.RemoteIpAddress =
                IPAddress.TryParse(context.Request.Headers[ClientIpHeader], out var testIp) ? testIp : ExternalClientIp;
            return nextMiddleware(context);
        });
        next(app);
    };
}
