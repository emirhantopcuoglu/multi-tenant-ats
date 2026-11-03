using Ats.Modules.Notifications.Infrastructure;
using Ats.Shared.Contracts.Notifications;

namespace Ats.UnitTests.Notifications;

public sealed class InterviewReminderIdentityTests
{
    private static readonly DateTime OriginalStart = new(2026, 11, 4, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void should_treat_a_redelivered_reminder_as_the_same_one()
    {
        var reminder = Reminder(OriginalStart, "DayBefore");

        Assert.Equal(InterviewReminderIdentity.For(reminder), InterviewReminderIdentity.For(reminder with { }));
    }

    // Day-before went out for 10:00, then the interview moved to 15:00 the same day. The reminder for
    // 15:00 used to be dropped as a repeat, leaving the candidate with only the old time.
    [Fact]
    public void should_treat_the_reminder_for_a_rescheduled_time_as_a_new_one()
    {
        var beforeReschedule = Reminder(OriginalStart, "DayBefore");
        var afterReschedule = beforeReschedule with { ScheduledAtUtc = OriginalStart.AddHours(5) };

        Assert.NotEqual(InterviewReminderIdentity.For(beforeReschedule), InterviewReminderIdentity.For(afterReschedule));
    }

    [Fact]
    public void should_treat_the_two_reminder_kinds_as_different_ones()
    {
        Assert.NotEqual(
            InterviewReminderIdentity.For(Reminder(OriginalStart, "DayBefore")),
            InterviewReminderIdentity.For(Reminder(OriginalStart, "StartingSoon")));
    }

    private static InterviewReminderDueIntegrationEvent Reminder(DateTime scheduledAtUtc, string kind) =>
        new(Guid.Parse("6f1c2a52-8a0e-4c4e-9d8e-2f3b5a7c9d01"), Guid.NewGuid(), Guid.NewGuid(), "Staff Engineer",
            Guid.NewGuid(), Guid.NewGuid(), "remi@acme.test", "Remi", "Technical",
            scheduledAtUtc, 30, RoomToken: null, kind, Guid.NewGuid());
}
