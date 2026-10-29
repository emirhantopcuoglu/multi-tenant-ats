using System.Net;
using Ats.Shared.Kernel;

namespace Ats.Modules.Notifications.Infrastructure;

// Shared by the rejection and hired emails: the title is bold, the fallback is plain text.
internal static class RolePhrase
{
    // The title is encoded before the markup is added.
    public static string For(string? jobTitle, IEmailTextProvider emailText, string language) =>
        string.IsNullOrWhiteSpace(jobTitle)
            ? emailText.Get(EmailTextKeys.Application.FallbackRole, language)
            : $"<strong>{WebUtility.HtmlEncode(jobTitle)}</strong>";
}
