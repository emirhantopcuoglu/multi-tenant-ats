namespace Ats.Shared.Contracts.Notifications;

// Only the in-app notification consumes it, so it has no contact fields.
public sealed record ApplicationCvDownloadedIntegrationEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    Guid? CandidateAccountId,
    Guid TenantId);
