namespace Ats.Shared.Contracts.Notifications;

// Published onto the message bus when a candidate is hired, and consumed by the Notifications
// module to email them the good news. Like ApplicationRejectedIntegrationEvent it lives in the
// neutral Contracts assembly so publisher and consumer never reference each other, and it is
// self-contained: every field the consumer needs travels in the message.
public sealed record ApplicationHiredIntegrationEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    string CandidateEmail,
    string CandidateFirstName,
    Guid TenantId);
