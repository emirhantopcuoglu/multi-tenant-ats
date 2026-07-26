using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Ats.Shared.Kernel;

namespace Ats.Modules.Notifications.Infrastructure;

// Formatting shared by the interview email consumers. Extracted once there were three of them
// repeating the same date format and the same PascalCase humanization — a divergence here would
// mean the same interview reads differently across the invitation, the move and the cancellation.
//
// Public rather than internal so the wording can be tested without a ConsumeContext and without an
// InternalsVisibleTo just for tests — the reason ClosingFor was public before it moved here.
public static partial class InterviewEmailFormatting
{
    // UTC is stated explicitly rather than converted: the system has no per-candidate timezone, so
    // an unlabelled local-looking time would be a guess presented as fact.
    //
    // The pattern and the culture both come from the recipient's language, and they have to move
    // together: the pattern decides field order and the 12/24-hour clock, the culture supplies the
    // day and month names. One without the other produces "Pazartesi, January 5" — half translated,
    // which reads worse than either language on its own.
    public static string FormatUtc(DateTime instant, IEmailTextProvider emailText, string language)
    {
        var pattern = emailText.Get(EmailTextKeys.Interview.DateFormat, language);
        return instant.ToString(pattern, CultureInfo.GetCultureInfo(language));
    }

    // The interview type as the candidate should read it. Types this build knows about are
    // translated; anything else — an older consumer against a newer producer — falls back to
    // splitting the PascalCase name, which is still readable and beats failing an otherwise fine
    // email over one word.
    public static string TypeName(string interviewType, IEmailTextProvider emailText, string language) =>
        EmailTextKeys.Interview.Types.Contains(interviewType)
            ? emailText.Get(EmailTextKeys.Interview.TypePrefix + interviewType, language)
            : HumanizeType(interviewType);

    // The sentence that answers the only question a cancellation notice must answer: is another
    // invitation coming? An unrecognised reason gets the neutral wording for the same reason as
    // above — a slightly vague email beats a poisoned message and no email at all.
    public static string CancellationClosing(string reason, IEmailTextProvider emailText, string language) =>
        EmailTextKeys.Interview.CancelReasons.Contains(reason)
            ? emailText.Get(EmailTextKeys.Interview.CancelReasonPrefix + reason, language)
            : emailText.Get(EmailTextKeys.Interview.UnknownCancelReason, language);

    // "PhoneScreen" -> "Phone Screen". The pattern is a zero-width lookaround (matches the boundary,
    // consumes nothing), so the replacement is a plain space, not a backreference.
    public static string HumanizeType(string value) => PascalCaseBoundary().Replace(value, " ");

    // The token is URL-safe base64 by construction (see Interview.GenerateRoomToken) so it cannot
    // contain HTML-unsafe characters, but it is encoded anyway — the same rule applied to every
    // other field rather than an exception someone has to remember.
    public static string JoinLine(
        string baseUrl, string roomToken, bool unchanged, IEmailTextProvider emailText, string language)
    {
        var url = $"{baseUrl}/{WebUtility.HtmlEncode(roomToken)}";
        var key = unchanged
            ? EmailTextKeys.Interview.JoinLineUnchanged
            : EmailTextKeys.Interview.JoinLine;

        return emailText.Get(key, language, url);
    }

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
    private static partial Regex PascalCaseBoundary();
}
