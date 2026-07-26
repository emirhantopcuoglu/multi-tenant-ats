using System.Net;
using Ats.Shared.Kernel;

namespace Ats.Modules.Notifications.Infrastructure;

// How a job title is referred to inside a sentence in the rejection and hired emails. Extracted
// because both of them make the same decision, and because that decision is easy to get subtly
// wrong: the fallback is plain text while the title is wrapped in <strong>, so a naive copy in one
// of the two would either bold the fallback or stop bolding the title.
internal static class RolePhrase
{
    // The title comes from a recruiter, so it is untrusted in an HTML email and is encoded before
    // the markup is wrapped around it. A missing title falls back to a neutral phrase rather than an
    // empty <strong></strong>, so the sentence still reads naturally without announcing the gap.
    public static string For(string? jobTitle, IEmailTextProvider emailText, string language) =>
        string.IsNullOrWhiteSpace(jobTitle)
            ? emailText.Get(EmailTextKeys.Application.FallbackRole, language)
            : $"<strong>{WebUtility.HtmlEncode(jobTitle)}</strong>";
}
