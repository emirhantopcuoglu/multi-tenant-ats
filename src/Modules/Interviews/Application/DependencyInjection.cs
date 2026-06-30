using Ats.Shared.Contracts.Interviews;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Ats.Modules.Interviews.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddInterviewsApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(
            typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Cross-module read port: lets the API compose interview counts for the dashboard.
        services.AddScoped<IInterviewDirectory, InterviewDirectory>();

        return services;
    }
}
