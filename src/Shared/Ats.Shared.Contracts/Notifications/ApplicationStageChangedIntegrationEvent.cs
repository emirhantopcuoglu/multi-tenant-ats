namespace Ats.Shared.Contracts.Notifications;

// Self-contained, including the stage names, which only the Applications module can resolve.
// CandidateAccountId is null for applications made before candidate accounts; in-app consumers
// skip those.
public sealed record ApplicationStageChangedIntegrationEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    Guid? CandidateAccountId,
    string CandidateEmail,
    string CandidateFirstName,
    Guid FromStageId,
    string FromStageName,
    Guid ToStageId,
    string ToStageName,
    Guid TenantId);
