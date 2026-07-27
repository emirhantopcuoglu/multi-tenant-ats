namespace Ats.Shared.Kernel;

// Building blocks for object-storage keys. The layout of a key belongs to its caller (see
// IFileStorage), but the one part that must never be improvised is how a user-supplied file name
// enters it — so that rule lives here and is shared, rather than being reimplemented per module
// where one copy would eventually drift from the other.
public static class StorageKey
{
    // The original file name is attacker-controlled: strip any path and keep only safe characters,
    // so it cannot escape its key prefix ("../"), smuggle in separators, or address a different
    // object than the caller intended.
    public static string SanitizeFileName(string fileName)
    {
        var nameOnly = Path.GetFileName(fileName);
        var safe = new string(nameOnly
            .Where(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_')
            .ToArray());

        return string.IsNullOrWhiteSpace(safe) ? FallbackFileName : safe;
    }

    // Used when sanitizing leaves nothing behind — a name of only non-Latin characters, say. The
    // key still has to be unique and readable, and the guid in front of it already provides the
    // uniqueness.
    public const string FallbackFileName = "cv";
}
