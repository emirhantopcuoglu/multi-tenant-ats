using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ats.Modules.Jobs.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddJobsApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(
            typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
