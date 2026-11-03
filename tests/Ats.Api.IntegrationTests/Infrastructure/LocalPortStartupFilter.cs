using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Ats.Api.IntegrationTests.Infrastructure;

// TestServer has no real listener, so LocalPort is always 0.
public sealed class LocalPortStartupFilter : IStartupFilter
{
    private readonly int _port;

    public LocalPortStartupFilter(int port)
    {
        _port = port;
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            context.Connection.LocalPort = _port;
            return nextMiddleware(context);
        });
        next(app);
    };
}
