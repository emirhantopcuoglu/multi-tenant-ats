namespace Ats.ArchitectureTests;

/// <summary>
/// The load-bearing rule of this modular monolith: a module owns its tables and its types, and
/// reaches other modules only through Ats.Shared.Contracts and the bus. The moment one module
/// references another's assembly, the seam that makes the modules separately reasonable — and
/// separately extractable — is gone, and nothing at compile time complains.
/// </summary>
public class ModuleBoundaryTests
{
    // Hardcoded on purpose. Every other rule in this project is "no assembly does X", which passes
    // trivially when no assembly was loaded. This test is what makes the rest mean something, and
    // adding a module has to be a deliberate edit here.
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
        // These two sit at the bottom: the kernel holds primitives every layer may use, and
        // contracts holds the integration events modules exchange. Both are copied into every
        // module's dependency graph, so anything they pull in is pulled in everywhere.
        var violations =
            from assembly in ModuleGraph.All
            let name = ModuleGraph.NameOf(assembly)
            where name is ModuleGraph.Kernel or ModuleGraph.Contracts
            from reference in ModuleGraph.SolutionReferencesOf(assembly)
            select $"{name} -> {reference}";

        ArchitectureAssert.NoViolations("Kernel/Contracts depending on other solution assemblies", violations);
    }
}
