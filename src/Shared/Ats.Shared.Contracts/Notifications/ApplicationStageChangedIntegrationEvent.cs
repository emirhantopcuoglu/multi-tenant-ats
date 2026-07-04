namespace Ats.Shared.Contracts.Notifications;

// Published onto the message bus when a recruiter moves an application to a new pipeline stage.
// Nothing consumes it yet: it is the raw material for the notification backbone — the in-app
// notification writer and the stage-change email will both subscribe to it.
//
// Like the other events in this namespace it is self-contained: candidate contact, job title and
// the human-readable stage names all travel in the message, so a consumer never has to reach back
// into the Applications module to describe the transition. Stage names are resolved by the
// publisher because only the Applications module can — the pipeline lives in its schema.
public sealed record ApplicationStageChangedIntegrationEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    string CandidateEmail,
    string CandidateFirstName,
    Guid FromStageId,
    string FromStageName,
    Guid ToStageId,
    string ToStageName,
    Guid TenantId);
