using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Carries what the email needs, but not the rejection reason, which is internal.
public sealed record ApplicationRejectedEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    string CandidateEmail,
    string CandidateFirstName,
    Guid TenantId) : INotification;
