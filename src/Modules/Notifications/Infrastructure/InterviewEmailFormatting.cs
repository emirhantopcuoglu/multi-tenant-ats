using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Ats.Modules.Notifications.Infrastructure;

// Formatting shared by the interview email consumers. Extracted once there were three of them
// repeating the same date format and the same PascalCase humanization — a divergence here would
// mean the same interview reads differently across the invitation, the move and the cancellation.
internal static partial class InterviewEmailFormatting
{
    // UTC is stated explicitly rather than converted: the system has no per-candidate timezone, so
    // an unlabelled local-looking time would be a guess presented as fact.
    public static string FormatUtc(DateTime instant) =>
        instant.ToString("dddd, MMMM d, yyyy 'at' h:mm tt 'UTC'", CultureInfo.InvariantCulture);

    // "PhoneScreen" -> "Phone Screen". The pattern is a zero-width lookaround (matches the boundary,
    // consumes nothing), so the replacement is a plain space, not a backreference.
    public static string HumanizeType(string value) => PascalCaseBoundary().Replace(value, " ");

    // The token is URL-safe base64 by construction (see Interview.GenerateRoomToken) so it cannot
    // contain HTML-unsafe characters, but it is encoded anyway — the same rule applied to every
    // other field rather than an exception someone has to remember.
    public static string JoinLine(string baseUrl, string roomToken, bool unchanged)
    {
        var url = $"{baseUrl}/{WebUtility.HtmlEncode(roomToken)}";
        var lead = unchanged
            ? "Your interview room link is unchanged"
            : "Join the interview room here when it opens";

        return $"""<p>{lead}: <a href="{url}">{url}</a></p>""";
    }

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
    private static partial Regex PascalCaseBoundary();
}
