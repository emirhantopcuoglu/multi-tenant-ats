using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;

namespace Ats.Api.Extensions;

public static class CorsExtensions
{
    // CORS for the SPA (Sprint 8.1). The front-end (Ats.Web) runs on a different origin than the API, so
    // the browser blocks its requests unless the API allows that origin. The allowed origins come from the
    // "Cors" section so each environment lists its own front-end without a code change. AllowCredentials is
    // required because the refresh flow carries credentials cross-origin; the CORS spec then forbids a
    // wildcard origin, which is why AllowedOrigins is an explicit list. Retry-After is exposed so the SPA
    // can read the rate limiter's back-off hint (cross-origin responses hide non-safelisted headers by default).
    public static IHostApplicationBuilder AddCorsForSpa(this IHostApplicationBuilder builder)
    {
        var corsOptions = builder.Configuration
            .GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

        builder.Services.AddCors(options =>
            options.AddPolicy(CorsPolicies.Spa, policy => policy
                .WithOrigins(corsOptions.AllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .WithExposedHeaders("Retry-After")));

        return builder;
    }
}
