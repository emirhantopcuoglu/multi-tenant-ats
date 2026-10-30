namespace Ats.Shared.Contracts.Notifications;

// Only the in-app notification consumes it, so it has no contact fields. CandidateAccountId is
// null for applications made before candidate accounts.
public sealed record ApplicationViewedIntegrationEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    Guid? CandidateAccountId,
    Guid TenantId);
