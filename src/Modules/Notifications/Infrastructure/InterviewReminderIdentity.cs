using Ats.Shared.Contracts.Notifications;

namespace Ats.Modules.Notifications.Infrastructure;

// Which reminders count as the same one, shared by the email and in-app consumers so they agree.
// Includes the start time, so a reschedule's reminder isn't dropped as a repeat of the old one.
// Ticks, because the DateTimeKind can change when the message is deserialized.
public static class InterviewReminderIdentity
{
    public static string For(InterviewReminderDueIntegrationEvent message) =>
        $"{message.InterviewId}:{message.Kind}:{message.ScheduledAtUtc.Ticks}";
}
