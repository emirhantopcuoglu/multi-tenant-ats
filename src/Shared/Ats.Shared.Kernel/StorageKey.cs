namespace Ats.Shared.Kernel;

// Shared so every module sanitizes user-supplied file names the same way.
public static class StorageKey
{
    // Strips paths and unsafe characters so the name can't escape its prefix.
    public static string SanitizeFileName(string fileName)
    {
        var nameOnly = Path.GetFileName(fileName);
        var safe = new string(nameOnly
            .Where(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_')
            .ToArray());

        return string.IsNullOrWhiteSpace(safe) ? FallbackFileName : safe;
    }

    // Used when nothing is left after sanitizing; the guid prefix keeps it unique.
    public const string FallbackFileName = "cv";
}
