using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Carries what the email needs, so the consumer doesn't load this module's data.
public sealed record ApplicationHiredEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    string CandidateEmail,
    string CandidateFirstName,
    Guid TenantId) : INotification;
