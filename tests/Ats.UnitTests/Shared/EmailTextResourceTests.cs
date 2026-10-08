using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ats.Shared.Infrastructure;
using Ats.Shared.Kernel;

namespace Ats.UnitTests.Shared;

// The safety net behind moving email wording out of C#. A missing or mistyped key no longer fails to
// compile, and the failure it causes instead — an exception thrown deep inside a MassTransit consumer
// while a candidate waits for a rejection letter — is a bad place to find out. These tests move that
// discovery to the build.
//
// They read the shipped JSON directly rather than only through the provider, because the provider
// falls back to English by design: a Turkish file missing half its keys would keep every
// provider-level assertion green while sending English to Turkish candidates.
public class EmailTextResourceTests
{
    private const string ResourceFolder = "EmailText";

    // Matches the placeholders string.Format will substitute: "{0}", "{12}". The alignment/format
    // suffixes ("{0,-5:N2}") are not matched because the wording does not use them, and a translator
    // who introduces one should see this test fail rather than have it quietly accepted.
    private static readonly Regex Placeholder = new(@"\{(\d+)\}", RegexOptions.Compiled);

    private static readonly FrozenDictionary<string, FrozenDictionary<string, string>> Resources =
        SupportedLanguages.All.ToFrozenDictionary(
            language => language,
            Load,
            StringComparer.Ordinal);

    [Fact]
    public void Every_declared_key_should_exist_in_every_language()
    {
        var missing = new List<string>();

        foreach (var key in DeclaredKeys())
        {
            foreach (var (language, texts) in Resources)
            {
                if (!texts.ContainsKey(key))
                    missing.Add($"{language}: {key}");
            }
        }

        Assert.Empty(missing);
    }

    [Fact]
    public void Every_resource_entry_should_be_declared_as_a_key()
    {
        // The other direction: an entry nobody reads is dead weight that a translator still pays to
        // maintain, and it usually means a call site was renamed and the JSON was not.
        var declared = DeclaredKeys().ToHashSet(StringComparer.Ordinal);

        var orphans = Resources[SupportedLanguages.Default].Keys
            .Where(key => !declared.Contains(key))
            .ToList();

        Assert.Empty(orphans);
    }

    [Fact]
    public void Translations_should_use_the_same_placeholders_as_english()
    {
        // The failure this catches is the expensive one: a Turkish body that drops {1} silently loses
        // the job title, and one that invents {7} throws FormatException at send time — for one
        // language only, which is exactly the kind of bug that reaches production.
        var english = Resources[SupportedLanguages.Default];
        var mismatches = new List<string>();

        foreach (var (language, texts) in Resources)
        {
            if (language == SupportedLanguages.Default)
                continue;

            foreach (var (key, text) in texts)
            {
                if (!english.TryGetValue(key, out var source))
                    continue;

                if (!PlaceholdersOf(text).SetEquals(PlaceholdersOf(source)))
                    mismatches.Add($"{language}: {key}");
            }
        }

        Assert.Empty(mismatches);
    }

    [Fact]
    public void No_text_should_be_blank()
    {
        var blanks = Resources
            .SelectMany(entry => entry.Value.Select(text => (Language: entry.Key, text.Key, text.Value)))
            .Where(entry => string.IsNullOrWhiteSpace(entry.Value))
            .Select(entry => $"{entry.Language}: {entry.Key}")
            .ToList();

        Assert.Empty(blanks);
    }

    [Fact]
    public void An_unknown_language_should_be_served_english_rather_than_throw()
    {
        var provider = new JsonEmailTextProvider();

        var served = provider.Get(EmailTextKeys.Application.FallbackRole, "klingon");
        var english = provider.Get(EmailTextKeys.Application.FallbackRole, SupportedLanguages.English);

        Assert.Equal(english, served);
    }

    [Fact]
    public void An_unknown_key_should_throw_rather_than_send_a_blank_email()
    {
        var provider = new JsonEmailTextProvider();

        Assert.Throws<KeyNotFoundException>(
            () => provider.Get("candidate.thisKeyDoesNotExist", SupportedLanguages.English));
    }

    private static HashSet<string> PlaceholdersOf(string text) =>
        Placeholder.Matches(text).Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);

    // Walks EmailTextKeys by reflection: every string constant is a key, and the two prefix constants
    // are expanded with the value lists that sit beside them. Reflection rather than a hand-written
    // list because a hand-written list is the thing that goes stale.
    private static IEnumerable<string> DeclaredKeys()
    {
        var prefixes = new[]
        {
            EmailTextKeys.Interview.TypePrefix,
            EmailTextKeys.Interview.CancelReasonPrefix,
        };

        foreach (var nested in typeof(EmailTextKeys).GetNestedTypes(BindingFlags.Public))
        {
            var constants = nested
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue()!);

            foreach (var value in constants)
            {
                // A prefix is not a key on its own — it is completed below.
                if (!prefixes.Contains(value, StringComparer.Ordinal))
                    yield return value;
            }
        }

        foreach (var type in EmailTextKeys.Interview.Types)
            yield return EmailTextKeys.Interview.TypePrefix + type;

        foreach (var reason in EmailTextKeys.Interview.CancelReasons)
            yield return EmailTextKeys.Interview.CancelReasonPrefix + reason;
    }

    private static FrozenDictionary<string, string> Load(string language)
    {
        var assembly = typeof(JsonEmailTextProvider).Assembly;
        var resourceName = $"{assembly.GetName().Name}.{ResourceFolder}.EmailText.{language}.json";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' was not found.");

        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!
            .ToFrozenDictionary(StringComparer.Ordinal);
    }
}
