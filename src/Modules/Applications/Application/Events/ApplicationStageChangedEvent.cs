using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Carries the stage names because only the handler has the pipeline loaded. CandidateAccountId
// is null for applications made before candidate accounts.
public sealed record ApplicationStageChangedEvent(
    Guid ApplicationId,
    Guid JobId,
    string JobTitle,
    Guid CandidateId,
    Guid? CandidateAccountId,
    string CandidateEmail,
    string CandidateFirstName,
    Guid FromStageId,
    string FromStageName,
    Guid ToStageId,
    string ToStageName,
    Guid TenantId) : INotification;
