using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Ats.ArchitectureTests;

/// <summary>
/// Where a type lives is the fastest thing a reader uses to guess what it may do. These rules pin
/// the three placements the codebase already relies on, so the guess stays correct.
/// </summary>
public class ConventionTests
{
    [Fact]
    public void A_request_handler_should_live_in_the_application_layer()
    {
        var violations = ConcreteTypesOutside(
            Layer.Application,
            type => ImplementsOpenInterface(type, typeof(IRequestHandler<,>)));

        ArchitectureAssert.NoViolations("MediatR handlers outside the Application layer", violations);
    }

    [Fact]
    public void A_controller_should_live_in_the_api_layer()
    {
        // The API host is allowed its own controllers: it owns the endpoints that read across
        // modules, such as the dashboard.
        var violations = ConcreteTypesOutside(
            Layer.Api,
            typeof(ControllerBase).IsAssignableFrom,
            ModuleGraph.Host);

        ArchitectureAssert.NoViolations("Controllers outside the Api layer", violations);
    }

    [Fact]
    public void A_message_consumer_should_live_in_the_infrastructure_layer()
    {
        // Consumers are the bus's entry point into a module and write through its DbContext, which
        // makes them infrastructure — the same reason a controller is not application code.
        var violations = ConcreteTypesOutside(
            Layer.Infrastructure,
            type => ImplementsOpenInterface(type, typeof(IConsumer<>)));

        ArchitectureAssert.NoViolations("MassTransit consumers outside the Infrastructure layer", violations);
    }

    private static IEnumerable<string> ConcreteTypesOutside(
        Layer expectedLayer,
        Func<Type, bool> isSubject,
        string? alsoAllowedAssembly = null) =>
        from assembly in ModuleGraph.All
        let name = ModuleGraph.NameOf(assembly)
        where ModuleGraph.LayerOf(assembly) != expectedLayer && name != alsoAllowedAssembly
        from type in ModuleGraph.TypesIn(assembly)
        where type is { IsClass: true, IsAbstract: false } && isSubject(type)
        select $"{type.FullName} ({name})";

    private static bool ImplementsOpenInterface(Type type, Type openInterface) =>
        type.GetInterfaces().Any(implemented =>
            implemented.IsGenericType && implemented.GetGenericTypeDefinition() == openInterface);
}
