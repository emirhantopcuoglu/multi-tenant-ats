namespace Ats.Shared.Kernel;

// Supplies the wording of a transactional email in the recipient's language. A port in the kernel
// next to IEmailSender, and for the same reason: every module sends mail, so neither the abstraction
// nor the resource files may belong to one of them.
//
// Deliberately not IStringLocalizer. That interface is built around .resx, and an email body is a
// small HTML document — reviewing a paragraph of markup XML-escaped inside a .resx entry is far
// worse than reading it in a JSON string, and a diff of a reworded email should show the wording.
public interface IEmailTextProvider
{
    // Returns the text for `key` in `language`, falling back to English when the language has no
    // entry, so a half-translated resource file degrades to a readable email rather than a blank one.
    //
    // `arguments` are substituted with string.Format. Callers pass values that are already
    // HTML-encoded: the resource text is trusted (it is source code), the values interpolated into
    // it are not. Keeping the encoding at the call site rather than here means the untrusted value
    // is encoded once, at the point where it is obvious that it came from a user.
    string Get(string key, string language, params object[] arguments);
}
