namespace Ats.Shared.Kernel;

// Email wording in the recipient's language. Not IStringLocalizer: bodies are HTML, which is
// much easier to review in JSON than escaped inside .resx.
public interface IEmailTextProvider
{
    // Falls back to English for a missing key. Arguments must already be HTML-encoded by the caller.
    string Get(string key, string language, params object[] arguments);
}
