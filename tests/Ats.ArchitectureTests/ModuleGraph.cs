using System.Reflection;

namespace Ats.ArchitectureTests;

/// <summary>
/// The layers every module is split into, ordered from the centre outwards.
/// </summary>
internal enum Layer
{
    Domain,
    Application,
    Infrastructure,
    Api,
}

/// <summary>
/// A reflection view over the compiled solution: which assembly belongs to which module and layer,
/// and what each one actually depends on.
/// </summary>
/// <remarks>
/// Rules are written against compiled references rather than the .csproj files, so they measure real
/// coupling: an unused ProjectReference is dropped by the compiler and stays invisible here. The
/// trade-off is that a reference used only for an inlined constant can also disappear — rare enough
/// to accept, and it would not be coupling worth failing a build over.
/// </remarks>
internal static class ModuleGraph
{
    public const string Kernel = "Ats.Shared.Kernel";
    public const string Contracts = "Ats.Shared.Contracts";
    public const string SharedInfrastructure = "Ats.Shared.Infrastructure";
    public const string Host = "Ats.Api";

    private const string SolutionPrefix = "Ats.";
    private const string ModulePrefix = "Ats.Modules.";
    private const int ModuleSegment = 2;
    private const int LayerSegment = 3;

    // Assemblies are read from the test output folder instead of AppDomain.CurrentDomain, because
    // the runtime loads an assembly only once a type inside it is touched — a module no test happens
    // to reference would silently escape every rule below.
    private static readonly Lazy<IReadOnlyList<Assembly>> LoadedAssemblies = new(LoadFromOutputFolder);

    public static IReadOnlyList<Assembly> All => LoadedAssemblies.Value;

    public static IEnumerable<Assembly> ModuleAssemblies => All.Where(assembly => ModuleOf(assembly) is not null);

    public static string NameOf(Assembly assembly) => assembly.GetName().Name!;

    public static string? ModuleOf(Assembly assembly) => ModuleOf(NameOf(assembly));

    /// <summary>"Ats.Modules.Jobs.Application" is the Jobs module; anything else belongs to no module.</summary>
    public static string? ModuleOf(string assemblyName) => SegmentOf(assemblyName, ModuleSegment);

    public static Layer? LayerOf(Assembly assembly) => LayerOf(NameOf(assembly));

    public static Layer? LayerOf(string assemblyName) =>
        Enum.TryParse<Layer>(SegmentOf(assemblyName, LayerSegment), out var layer) ? layer : null;

    /// <summary>The solution's own dependencies; third-party and framework references are left out.</summary>
    public static IEnumerable<string> SolutionReferencesOf(Assembly assembly) =>
        ReferencesOf(assembly).Where(name => name.StartsWith(SolutionPrefix, StringComparison.Ordinal));

    public static IEnumerable<string> ReferencesOf(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(reference => reference.Name!);

    public static IEnumerable<Type> TypesIn(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            // A type whose dependency is missing at runtime cannot break a boundary rule, so the
            // ones that did load are still worth inspecting.
            return exception.Types.OfType<Type>();
        }
    }

    private static string? SegmentOf(string assemblyName, int index)
    {
        if (!assemblyName.StartsWith(ModulePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var segments = assemblyName.Split('.');
        return segments.Length > index ? segments[index] : null;
    }

    private static IReadOnlyList<Assembly> LoadFromOutputFolder()
    {
        var testAssembly = NameOf(typeof(ModuleGraph).Assembly);

        return Directory
            .EnumerateFiles(AppContext.BaseDirectory, SolutionPrefix + "*.dll")
            .Where(path => Path.GetFileNameWithoutExtension(path) != testAssembly)
            .Select(Assembly.LoadFrom)
            .ToList();
    }
}
