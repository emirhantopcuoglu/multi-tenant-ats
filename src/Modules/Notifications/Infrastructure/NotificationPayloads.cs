using System.Text.Json;

namespace Ats.Modules.Notifications.Infrastructure;

// Payload shapes, one per NotificationType. Facts only, no rendered text. There is no field for
// recruiter notes or rejection reasons, so they can't leak.
public static class NotificationPayloads
{
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
        // Just the token: the SPA builds the room link itself. Null for phone screens.
        string? RoomToken);

    // Both times, so the client can show what changed.
    public sealed record InterviewRescheduled(
        Guid ApplicationId,
        string JobTitle,
        string InterviewType,
        DateTime PreviousScheduledAtUtc,
        DateTime ScheduledAtUtc,
        int DurationMinutes,
        string? RoomToken);

    // A closed set, so the client can say whether a new invitation is coming.
    public sealed record InterviewCancelled(
        Guid ApplicationId,
        string JobTitle,
        string InterviewType,
        DateTime ScheduledAtUtc,
        string Reason);

    // Same fields as InterviewScheduled plus the kind; unknown kinds render the generic row.
    public sealed record InterviewReminder(
        Guid ApplicationId,
        string JobTitle,
        string InterviewType,
        DateTime ScheduledAtUtc,
        int DurationMinutes,
        string? RoomToken,
        string ReminderKind);

    public sealed record ApplicationViewed(Guid ApplicationId, string JobTitle);

    public sealed record ApplicationCvDownloaded(Guid ApplicationId, string JobTitle);

    public sealed record NewApplication(
        Guid ApplicationId, string JobTitle, string CandidateFirstName, string CandidateLastName);

    public static string Serialize<T>(T payload) => JsonSerializer.Serialize(payload, SerializerOptions);
}
