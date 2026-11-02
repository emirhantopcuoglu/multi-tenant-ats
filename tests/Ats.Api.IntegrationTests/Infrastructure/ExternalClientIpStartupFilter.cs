using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Ats.Api.IntegrationTests.Infrastructure;

// TestServer leaves RemoteIpAddress null, and ForwardedHeaders trusts any header then. Kestrel always sets it.
public sealed class ExternalClientIpStartupFilter : IStartupFilter
{
    private static readonly IPAddress ExternalClientIp = IPAddress.Parse("198.51.100.10");

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            context.Connection.RemoteIpAddress = ExternalClientIp;
            return nextMiddleware(context);
        });
        next(app);
    };
}
