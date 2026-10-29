using Microsoft.Extensions.DependencyInjection;

namespace Ats.Modules.Notifications.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services)
    {
        // No validation pipeline: every input is server-derived (recipient from the token, ids from the
        // route). Out-of-range paging falls back to defaults in the handler.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        return services;
    }
}
