namespace Ats.Shared.Contracts.Notifications;

// Published onto the message bus the first time a company user opens an application. Only the
// in-app notification backbone consumes it today — there is no email planned for this signal, so
// unlike ApplicationRejectedIntegrationEvent/ApplicationStageChangedIntegrationEvent it carries no
// candidate contact fields; adding them now would be dead data with no consumer to read them.
//
// CandidateAccountId is the global marketplace account the notification is addressed to (nullable:
// applications submitted before candidate accounts existed have none, and consumers skip those).
public sealed record ApplicationViewedIntegrationEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    Guid? CandidateAccountId,
    Guid TenantId);
