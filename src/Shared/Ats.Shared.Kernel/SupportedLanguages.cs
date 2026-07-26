namespace Ats.Shared.Kernel;

// The set of languages the product speaks, mirroring SUPPORTED_LANGUAGES in the SPA's i18n layer.
// Lives in the shared kernel for the same reason as SupportedCountries: several modules consume it
// (Tenants and CandidateAccounts store a preference, Notifications reads one to pick email wording)
// and none of them should own it.
//
// Plain strings rather than an enum, again matching SupportedCountries: this only tightens an input
// boundary. The stored value stays a two-letter code, which is also what a CultureInfo lookup and an
// HTTP Accept-Language header speak, so nothing has to translate between representations.
public static class SupportedLanguages
{
    public const string English = "en";
    public const string Turkish = "tr";

    // English is the fallback everywhere: it is the language the resource files are authored in, so
    // it is the one guaranteed to have every key.
    public const string Default = English;

    public static readonly IReadOnlyList<string> All = [English, Turkish];

    public static bool IsSupported(string? code) =>
        code is not null && All.Contains(code, StringComparer.Ordinal);

    // Accepts anything a browser or an API client might send — "TR", "tr-TR", null — and answers
    // with a code this system actually has resources for. Never throws: an unrecognised language is
    // a reason to write English, not a reason to fail a registration.
    //
    // ToLowerInvariant, not ToLower: under a Turkish culture "I".ToLower() is "ı", so a
    // culture-sensitive lowercase would fail to match its own language code.
    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Default;

        var primary = code.Split('-')[0].ToLowerInvariant();
        return IsSupported(primary) ? primary : Default;
    }
}
