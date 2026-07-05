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
        string? Location);

    public static string Serialize<T>(T payload) => JsonSerializer.Serialize(payload, SerializerOptions);
}
