using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Raised on the first view only.
public sealed record ApplicationViewedEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    Guid? CandidateAccountId,
    Guid TenantId) : INotification;
