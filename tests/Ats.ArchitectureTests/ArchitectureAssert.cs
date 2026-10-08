namespace Ats.ArchitectureTests;

internal static class ArchitectureAssert
{
    /// <summary>
    /// Fails with every offender listed at once. A boundary is usually crossed in several places by
    /// the same change, and fixing them one failure per run is a waste of a build.
    /// </summary>
    public static void NoViolations(string rule, IEnumerable<string> violations)
    {
        var offenders = violations.ToList();

        Assert.True(
            offenders.Count == 0,
            $"{rule}{Environment.NewLine}{string.Join(Environment.NewLine, offenders.Select(offender => "  " + offender))}");
    }
}
