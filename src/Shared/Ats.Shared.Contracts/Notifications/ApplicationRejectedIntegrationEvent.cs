namespace Ats.Shared.Contracts.Notifications;

// Self-contained. The rejection reason is internal and isn't included.
public sealed record ApplicationRejectedIntegrationEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    string CandidateEmail,
    string CandidateFirstName,
    Guid TenantId);
