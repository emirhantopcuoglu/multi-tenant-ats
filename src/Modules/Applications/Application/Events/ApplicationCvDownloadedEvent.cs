using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Raised on the first CV download only.
public sealed record ApplicationCvDownloadedEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    Guid? CandidateAccountId,
    Guid TenantId) : INotification;
