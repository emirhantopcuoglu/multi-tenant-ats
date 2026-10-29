namespace Ats.Shared.Contracts.Notifications;

// Published by the reminder sweep. Same facts as InterviewScheduledIntegrationEvent plus Kind,
// which only changes the wording.
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
    // A string, so the contract doesn't carry the Interviews enum.
    string Kind,
    Guid TenantId);

// The allowed Kind values, so consumers don't reference the Interviews module.
public static class InterviewReminderKinds
{
    public const string DayBefore = "DayBefore";
    public const string StartingSoon = "StartingSoon";
}
