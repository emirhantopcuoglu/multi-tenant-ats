namespace Ats.Shared.Contracts.Notifications;

// Published onto the message bus when an application is rejected, and consumed by the Notifications
// module to email the candidate. Like ApplicationSubmittedIntegrationEvent it lives in the neutral
// Contracts assembly so publisher and consumer never reference each other.
//
// The message is self-contained: every field the consumer needs to build the email travels in it,
// so the consumer never loads another module's aggregates — and can run in a separate process once
// Notifications is extracted into its own service. The rejection reason is intentionally absent: it
// is an internal note and must not reach the candidate.
public sealed record ApplicationRejectedIntegrationEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    string CandidateEmail,
    string CandidateFirstName,
    Guid TenantId);
