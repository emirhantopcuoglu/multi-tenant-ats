namespace Ats.Shared.Contracts.Notifications;

// Self-contained: everything the consumers need is in the message.
public sealed record ApplicationSubmittedIntegrationEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    string CandidateEmail,
    string CandidateFirstName,
    string CandidateLastName,
    Guid TenantId);
