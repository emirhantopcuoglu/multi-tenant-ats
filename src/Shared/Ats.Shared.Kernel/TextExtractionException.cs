namespace Ats.Shared.Kernel;

/// <summary>
/// A file passed the format check but its content could not be read — a truncated upload, a
/// corrupted PDF, a ZIP that is not really a Word package.
/// </summary>
/// <remarks>
/// Its own type so a caller can tell this apart from a transient failure. The distinction decides
/// whether retrying is worth anything: a network blip is worth another attempt, a broken file is
/// broken the same way forever, and redelivering it only fills the error queue with messages that
/// can never succeed.
///
/// Thrown by the extractors rather than surfacing the library's own exception, so callers do not
/// have to reference PdfPig or reason about which BCL exception a malformed ZIP produces.
/// </remarks>
public sealed class TextExtractionException : Exception
{
    public TextExtractionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
