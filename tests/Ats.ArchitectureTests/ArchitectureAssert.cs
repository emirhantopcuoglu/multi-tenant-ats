namespace Ats.ArchitectureTests;

internal static class ArchitectureAssert
{
    /// <summary>Lists every offender at once instead of one per run.</summary>
    public static void NoViolations(string rule, IEnumerable<string> violations)
    {
        var offenders = violations.ToList();

        Assert.True(
            offenders.Count == 0,
            $"{rule}{Environment.NewLine}{string.Join(Environment.NewLine, offenders.Select(offender => "  " + offender))}");
    }
}
