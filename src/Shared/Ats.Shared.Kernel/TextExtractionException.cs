namespace Ats.Shared.Kernel;

/// <summary>
/// The file passed the format check but its content can't be read. Its own type so callers skip
/// it instead of retrying, and don't depend on library exception types.
/// </summary>
public sealed class TextExtractionException : Exception
{
    public TextExtractionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
