using System.Collections.Frozen;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Ats.Shared.Kernel;

namespace Ats.Shared.Infrastructure;

// Reads the email wording from EmailText/EmailText.<language>.json, embedded in this assembly.
//
// Embedded rather than copied to the output directory: the files are code, not configuration. An
// operator has no business editing an email body on a server, and shipping them inside the assembly
// removes a whole class of "works locally, blank email in the container" failures.
//
// Registered as a singleton. The files are parsed once at construction — one small JSON parse per
// language — and every dictionary is frozen afterwards, so all later reads are lock-free lookups on
// immutable state. FrozenDictionary rather than Dictionary because this is written once and read on
// every email for the life of the process, which is exactly the shape it optimises for.
public sealed class JsonEmailTextProvider : IEmailTextProvider
{
    private const string ResourceFolder = "EmailText";

    private readonly FrozenDictionary<string, FrozenDictionary<string, string>> _textsByLanguage;

    public JsonEmailTextProvider()
    {
        var assembly = typeof(JsonEmailTextProvider).Assembly;

        _textsByLanguage = SupportedLanguages.All
            .ToFrozenDictionary(
                language => language,
                language => Load(assembly, language),
                StringComparer.Ordinal);
    }

    public string Get(string key, string language, params object[] arguments)
    {
        var text = Resolve(key, SupportedLanguages.Normalize(language));

        // string.Format on a text with no placeholders is wasted work, and it would also throw on a
        // stray brace in wording that never meant to interpolate anything.
        if (arguments.Length == 0)
            return text;

        // InvariantCulture: the arguments arrive already formatted by the caller (dates through
        // InterviewEmailFormatting, which picks the culture deliberately), so the only values left
        // for Format to render are plain numbers, and those must not pick up a decimal comma here.
        return string.Format(CultureInfo.InvariantCulture, text, arguments);
    }

    private string Resolve(string key, string language)
    {
        if (_textsByLanguage[language].TryGetValue(key, out var translated))
            return translated;

        // A key the target language has not translated yet falls back to English rather than to the
        // key name: a partially translated file should degrade to a readable email, not a broken one.
        if (_textsByLanguage[SupportedLanguages.Default].TryGetValue(key, out var fallback))
            return fallback;

        // Missing from English too, which no translation gap can cause — it means a call site names
        // a key the resource file does not define. That is a bug in this build, so it is loud.
        // EmailTextResourceTests exists to make sure it is caught before a deploy rather than after.
        throw new KeyNotFoundException(
            $"Email text key '{key}' is missing from {ResourceFolder}/EmailText.{SupportedLanguages.Default}.json.");
    }

    private static FrozenDictionary<string, string> Load(Assembly assembly, string language)
    {
        var resourceName = $"{assembly.GetName().Name}.{ResourceFolder}.EmailText.{language}.json";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded email text resource '{resourceName}' was not found. "
                + "Check that the JSON file is included as an EmbeddedResource.");

        var texts = JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidOperationException($"Embedded email text resource '{resourceName}' is empty.");

        return texts.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
