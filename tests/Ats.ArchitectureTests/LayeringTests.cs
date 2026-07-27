namespace Ats.ArchitectureTests;

/// <summary>
/// Inside a module, dependencies point inwards: Api and Infrastructure know about Application,
/// Application knows about Domain, and Domain knows about nothing but the kernel. Most of the
/// backwards edges are already impossible (they would be circular project references), but the
/// sideways ones — Api reaching straight into Infrastructure, Domain pulling in EF Core — compile
/// fine and only show up later as a domain you cannot unit test.
/// </summary>
public class LayeringTests
{
    // Assembly-name prefixes for the things that talk to the outside world: databases, the bus, the
    // web stack. The domain is where the business rules live; if it needs one of these to run, the
    // rules can no longer be exercised without infrastructure standing up.
    private static readonly string[] InfrastructureFrameworks =
    [
        "Microsoft.EntityFrameworkCore",
        "Microsoft.AspNetCore",
        "Npgsql",
        "MassTransit",
        "MediatR",
        "Hangfire",
        "StackExchange.Redis",
        "MongoDB",
        "MailKit",
        "Minio",
    ];

    [Fact]
    public void A_layer_should_only_reference_what_its_layer_allows()
    {
        var violations =
            from assembly in ModuleGraph.ModuleAssemblies
            let module = ModuleGraph.ModuleOf(assembly)!
            let layer = ModuleGraph.LayerOf(assembly)
            where layer is not null
            let allowed = AllowedReferences(module, layer.Value)
            from reference in ModuleGraph.SolutionReferencesOf(assembly)
            // Cross-module references have their own rule and their own failure message; this one is
            // only about the direction of dependencies inside a module.
            where ModuleGraph.ModuleOf(reference) is null || ModuleGraph.ModuleOf(reference) == module
            where !allowed.Contains(reference)
            select $"{ModuleGraph.NameOf(assembly)} -> {reference}";

        ArchitectureAssert.NoViolations("References that break the layer direction", violations);
    }

    [Fact]
    public void A_domain_should_not_depend_on_infrastructure_frameworks()
    {
        var violations =
            from assembly in ModuleGraph.ModuleAssemblies
            where ModuleGraph.LayerOf(assembly) == Layer.Domain
            from reference in ModuleGraph.ReferencesOf(assembly)
            where InfrastructureFrameworks.Any(framework => reference.StartsWith(framework, StringComparison.Ordinal))
            select $"{ModuleGraph.NameOf(assembly)} -> {reference}";

        ArchitectureAssert.NoViolations("Domain assemblies depending on infrastructure", violations);
    }

    [Fact]
    public void An_application_layer_should_not_depend_on_the_web_stack()
    {
        // Application handlers are reached through MediatR and know nothing about HTTP: no
        // HttpContext, no ActionResult, no model binding. That is what lets the same handler be
        // driven by a controller today and a background consumer tomorrow.
        var violations =
            from assembly in ModuleGraph.ModuleAssemblies
            where ModuleGraph.LayerOf(assembly) == Layer.Application
            from reference in ModuleGraph.ReferencesOf(assembly)
            where reference.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal)
            select $"{ModuleGraph.NameOf(assembly)} -> {reference}";

        ArchitectureAssert.NoViolations("Application assemblies depending on the web stack", violations);
    }

    private static HashSet<string> AllowedReferences(string module, Layer layer) => layer switch
    {
        Layer.Domain => [ModuleGraph.Kernel],
        Layer.Application => [ModuleGraph.Kernel, ModuleGraph.Contracts, Assembly(module, Layer.Domain)],
        Layer.Infrastructure =>
        [
            ModuleGraph.Kernel,
            ModuleGraph.Contracts,
            ModuleGraph.SharedInfrastructure,
            Assembly(module, Layer.Domain),
            Assembly(module, Layer.Application),
        ],
        // Api may read the domain because its request and response records bind domain enums
        // directly (EmploymentType, InterviewType, NotificationKind). Copying those into the Api
        // layer would buy a mapping step and nothing else. What it may not do is reach past
        // Application into Infrastructure and query the database itself.
        Layer.Api =>
        [
            ModuleGraph.Kernel,
            ModuleGraph.Contracts,
            Assembly(module, Layer.Domain),
            Assembly(module, Layer.Application),
        ],
        _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, "Unknown layer"),
    };

    private static string Assembly(string module, Layer layer) => $"Ats.Modules.{module}.{layer}";
}
