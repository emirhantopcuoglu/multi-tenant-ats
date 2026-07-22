namespace Ats.Shared.Contracts.Notifications;

// Published onto the message bus when an application is submitted, and consumed by the
// Notifications module to email the candidate a confirmation. Integration events are the
// cross-module, out-of-process counterpart to in-module domain events; like IJobDirectory they
// live in the neutral Contracts assembly so publisher and consumer never reference each other.
//
// The message is self-contained: every field the consumer needs to build the email travels in it,
// so the consumer never loads another module's aggregates — and can run in a separate process once
// Notifications is extracted into its own service (Sprint 8).
// CandidateLastName was added alongside the new-application in-app notification (roadmap 3.2): the
// company recipient sees the applicant's full name, not just a first name.
public sealed record ApplicationSubmittedIntegrationEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    string CandidateEmail,
    string CandidateFirstName,
    string CandidateLastName,
    Guid TenantId);
