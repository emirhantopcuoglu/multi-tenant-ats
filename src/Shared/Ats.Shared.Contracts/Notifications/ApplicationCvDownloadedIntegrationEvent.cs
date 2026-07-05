namespace Ats.Shared.Contracts.Notifications;

// Published onto the message bus the first time a company user downloads an application's CV.
// Same shape and reasoning as ApplicationViewedIntegrationEvent: only the in-app notification
// backbone consumes it, so there is no candidate contact field to carry.
public sealed record ApplicationCvDownloadedIntegrationEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    Guid? CandidateAccountId,
    Guid TenantId);
