using System.Collections.Frozen;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Ats.Shared.Kernel;

namespace Ats.Shared.Infrastructure;

// Wording from the embedded EmailText.<language>.json files. Embedded because they are code, not
// config. Parsed once into frozen dictionaries; registered as a singleton.
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

        // Skips Format when there are no arguments, so a stray brace in plain text can't throw.
        if (arguments.Length == 0)
            return text;

        // Arguments arrive already formatted; invariant culture keeps numbers from getting a decimal comma.
        return string.Format(CultureInfo.InvariantCulture, text, arguments);
    }

    private string Resolve(string key, string language)
    {
        if (_textsByLanguage[language].TryGetValue(key, out var translated))
            return translated;

        // An untranslated key falls back to English.
        if (_textsByLanguage[SupportedLanguages.Default].TryGetValue(key, out var fallback))
            return fallback;

        // Missing in English too means a wrong key in code. EmailTextResourceTests catches it.
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
