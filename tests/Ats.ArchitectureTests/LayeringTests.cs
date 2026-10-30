namespace Ats.ArchitectureTests;

/// <summary>
/// Dependencies point inwards inside a module. Backwards edges would be circular references anyway;
/// these rules catch the sideways ones, like Api using Infrastructure or Domain using EF Core.
/// </summary>
public class LayeringTests
{
    // Prefixes of database, bus and web assemblies. The domain must not need them to run.
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
        // Cross-module references are skipped: they have their own rule and their own failure
        // message; this one is only about the direction of dependencies inside a module.
        var violations =
            from assembly in ModuleGraph.ModuleAssemblies
            let module = ModuleGraph.ModuleOf(assembly)!
            let layer = ModuleGraph.LayerOf(assembly)
            where layer is not null
            let allowed = AllowedReferences(module, layer.Value)
            from reference in ModuleGraph.SolutionReferencesOf(assembly)
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
        // Handlers run through MediatR and know nothing about HTTP, so a consumer can call them too.
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
        // Api may use the domain (request records bind domain enums) but not Infrastructure.
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
