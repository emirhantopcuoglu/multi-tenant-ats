namespace Ats.ArchitectureTests;

/// <summary>
/// A module reaches other modules only through Ats.Shared.Contracts and the bus. Nothing at compile
/// time stops one module referencing another, so this test does.
/// </summary>
public class ModuleBoundaryTests
{
    // Hardcoded: the other rules pass trivially if no assembly is loaded. Adding a module means editing
    // this list.
    private static readonly string[] ExpectedModules =
        ["Applications", "CandidateAccounts", "Interviews", "Jobs", "Notifications", "Tenants"];

    private static readonly string[] SharedAssemblies =
        [ModuleGraph.Kernel, ModuleGraph.Contracts, ModuleGraph.SharedInfrastructure];

    [Fact]
    public void Every_module_layer_and_shared_project_should_be_under_test()
    {
        var expected =
            from module in ExpectedModules
            from layer in Enum.GetValues<Layer>()
            select $"Ats.Modules.{module}.{layer}";

        var loaded = ModuleGraph.All.Select(ModuleGraph.NameOf).ToHashSet();

        ArchitectureAssert.NoViolations(
            "Assemblies missing from the architecture test run",
            expected.Concat(SharedAssemblies).Append(ModuleGraph.Host).Where(name => !loaded.Contains(name)));
    }

    [Fact]
    public void A_module_should_not_reference_another_module()
    {
        var violations =
            from assembly in ModuleGraph.ModuleAssemblies
            let module = ModuleGraph.ModuleOf(assembly)
            from reference in ModuleGraph.SolutionReferencesOf(assembly)
            let referencedModule = ModuleGraph.ModuleOf(reference)
            where referencedModule is not null && referencedModule != module
            select $"{ModuleGraph.NameOf(assembly)} -> {reference}";

        ArchitectureAssert.NoViolations("Cross-module references (use Ats.Shared.Contracts)", violations);
    }

    [Fact]
    public void A_shared_project_should_not_reference_a_module()
    {
        // Shared code is depended on by every module, so a reference pointing back at one of them
        // would drag that module into all the others.
        var violations =
            from assembly in ModuleGraph.All
            where SharedAssemblies.Contains(ModuleGraph.NameOf(assembly))
            from reference in ModuleGraph.SolutionReferencesOf(assembly)
            where ModuleGraph.ModuleOf(reference) is not null
            select $"{ModuleGraph.NameOf(assembly)} -> {reference}";

        ArchitectureAssert.NoViolations("Shared projects referencing a module", violations);
    }

    [Fact]
    public void The_kernel_and_contracts_should_stay_free_of_solution_dependencies()
    {
        // The kernel and contracts are in every module's dependency graph, so whatever they reference ends
        // up everywhere.
        var violations =
            from assembly in ModuleGraph.All
            let name = ModuleGraph.NameOf(assembly)
            where name is ModuleGraph.Kernel or ModuleGraph.Contracts
            from reference in ModuleGraph.SolutionReferencesOf(assembly)
            select $"{name} -> {reference}";

        ArchitectureAssert.NoViolations("Kernel/Contracts depending on other solution assemblies", violations);
    }
}
