using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Ats.Shared.Kernel;

namespace Ats.Modules.Notifications.Infrastructure;

// Shared by the interview emails so the same interview reads the same in each of them.
public static partial class InterviewEmailFormatting
{
    // Shown as UTC because there is no per-candidate timezone. Pattern and culture both follow the
    // language; mixing them gives half-translated dates.
    public static string FormatUtc(DateTime instant, IEmailTextProvider emailText, string language)
    {
        var pattern = emailText.Get(EmailTextKeys.Interview.DateFormat, language);
        return instant.ToString(pattern, CultureInfo.GetCultureInfo(language));
    }

    // Unknown types fall back to splitting the PascalCase name instead of failing the email.
    public static string TypeName(string interviewType, IEmailTextProvider emailText, string language) =>
        EmailTextKeys.Interview.Types.Contains(interviewType)
            ? emailText.Get(EmailTextKeys.Interview.TypePrefix + interviewType, language)
            : HumanizeType(interviewType);

    // Unknown reasons get the neutral wording.
    public static string CancellationClosing(string reason, IEmailTextProvider emailText, string language) =>
        EmailTextKeys.Interview.CancelReasons.Contains(reason)
            ? emailText.Get(EmailTextKeys.Interview.CancelReasonPrefix + reason, language)
            : emailText.Get(EmailTextKeys.Interview.UnknownCancelReason, language);

    // The pattern is a zero-width lookaround, so the replacement is a plain space.
    public static string HumanizeType(string value) => PascalCaseBoundary().Replace(value, " ");

    // Each email passes its own wording key; only the link is shared.
    public static string JoinLine(
        string baseUrl, string roomToken, string textKey, IEmailTextProvider emailText, string language)
    {
        var url = $"{baseUrl}/{WebUtility.HtmlEncode(roomToken)}";
        return emailText.Get(textKey, language, url);
    }

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
    private static partial Regex PascalCaseBoundary();
}
