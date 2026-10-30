namespace Ats.Shared.Kernel;

// Mirrors SUPPORTED_LANGUAGES in the SPA. Two-letter codes, the same format CultureInfo and
// Accept-Language use.
public static class SupportedLanguages
{
    public const string English = "en";
    public const string Turkish = "tr";

    // English always has every key.
    public const string Default = English;

    public static readonly IReadOnlyList<string> All = [English, Turkish];

    public static bool IsSupported(string? code) =>
        code is not null && All.Contains(code, StringComparer.Ordinal);

    // Accepts "TR", "tr-TR", null and so on, and never throws. ToLowerInvariant because under a
    // Turkish culture "I".ToLower() is "ı".
    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Default;

        var primary = code.Split('-')[0].ToLowerInvariant();
        return IsSupported(primary) ? primary : Default;
    }
}
