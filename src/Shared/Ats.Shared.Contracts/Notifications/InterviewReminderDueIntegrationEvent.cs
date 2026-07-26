namespace Ats.Shared.Contracts.Notifications;

// Published by the interview reminder sweep when a scheduled nudge falls due. Unlike its siblings
// this one is not caused by a user action — nobody clicked anything, the clock simply moved — which
// is why it is produced by a background job rather than by a command handler.
//
// Shaped like InterviewScheduledIntegrationEvent on purpose: a reminder repeats the invitation's
// facts, so consuming it must not need any field the invitation did not already carry. Kind is the
// only addition, and it decides the wording ("tomorrow" versus "starting now"), not the recipient
// or the routing.
public sealed record InterviewReminderDueIntegrationEvent(
    Guid InterviewId,
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    Guid? CandidateAccountId,
    string CandidateEmail,
    string CandidateFirstName,
    string InterviewType,
    DateTime ScheduledAtUtc,
    int DurationMinutes,
    string? RoomToken,
    // One of InterviewReminderKinds. A string, not the Interviews module's enum, for the same reason
    // InterviewType is a string here: a contract must not drag a module's domain types across the
    // boundary.
    string Kind,
    Guid TenantId);

// The values Kind may take, restated on the contract side so a consumer can branch on them without
// referencing the Interviews module. Kept next to the event rather than in the kernel because
// nothing outside this message has any use for them.
public static class InterviewReminderKinds
{
    public const string DayBefore = "DayBefore";
    public const string StartingSoon = "StartingSoon";
}
