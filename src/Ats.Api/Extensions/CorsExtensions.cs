using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;

namespace Ats.Api.Extensions;

public static class CorsExtensions
{
    // Origins come from the "Cors" section. AllowCredentials rules out a wildcard. Retry-After is
    // exposed so the SPA can read the rate limiter's hint.
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
