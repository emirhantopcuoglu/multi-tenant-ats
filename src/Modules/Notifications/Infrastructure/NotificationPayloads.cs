using System.Text.Json;

namespace Ats.Modules.Notifications.Infrastructure;

// The JSON shapes stored in Notification.Payload, one record per NotificationType. These are the
// module's contract with the web client: structured facts only, never pre-rendered sentences —
// the client picks the localized template by notification type and fills it from these fields, so
// the feed's language is a client concern. ApplicationId is in every shape because the click-through
// target is the candidate's application tracking page.
//
// Candidate-safety is structural, same principle as the integration events: there is no field for
// recruiter notes, internal rejection reasons or acting users, so they cannot leak by mapping bug.
public static class NotificationPayloads
{
    // camelCase on the wire so the client reads payload.jobTitle, matching the API's JSON style.
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public sealed record ApplicationStageChanged(
        Guid ApplicationId,
        string JobTitle,
        string FromStageName,
        string ToStageName);

    public sealed record InterviewScheduled(
        Guid ApplicationId,
        string JobTitle,
        string InterviewType,
        DateTime ScheduledAtUtc,
        int DurationMinutes,
        // Kept as a bare token, not a full URL: the in-app feed is rendered by the same SPA that
        // owns the /interview-room route, so it builds the link itself rather than duplicating the
        // absolute-URL config the email consumer needs for an external inbox.
        string RoomToken);

    public sealed record ApplicationViewed(Guid ApplicationId, string JobTitle);

    public sealed record ApplicationCvDownloaded(Guid ApplicationId, string JobTitle);

    // Company-side payload: raw name facts, not a pre-joined "Jane Doe" string, matching the
    // "structured facts only" rule above.
    public sealed record NewApplication(
        Guid ApplicationId, string JobTitle, string CandidateFirstName, string CandidateLastName);

    public static string Serialize<T>(T payload) => JsonSerializer.Serialize(payload, SerializerOptions);
}
