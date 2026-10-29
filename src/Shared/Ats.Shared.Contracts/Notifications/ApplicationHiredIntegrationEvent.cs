namespace Ats.Shared.Contracts.Notifications;

// Self-contained: everything the email needs is in the message.
public sealed record ApplicationHiredIntegrationEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    string CandidateEmail,
    string CandidateFirstName,
    Guid TenantId);
