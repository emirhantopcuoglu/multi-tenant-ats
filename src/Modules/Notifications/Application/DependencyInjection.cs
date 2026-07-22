using Microsoft.Extensions.DependencyInjection;

namespace Ats.Modules.Notifications.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services)
    {
        // No FluentValidation pipeline here, unlike the sibling modules: every command and query
        // in this module takes only server-derived values (the recipient comes from the JWT, ids
        // from the route), so there is no untrusted input shape for a validator to police. Paging
        // is clamped in the handler, matching the list queries elsewhere.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        return services;
    }
}
