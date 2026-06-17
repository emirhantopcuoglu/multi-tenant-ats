using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// Raised in-process after a recruiter moves an application to a new stage. Nothing consumes it
// yet; it becomes the trigger for stage-change notifications once the messaging layer is in
// place. Carries the from/to stage ids so a future consumer can describe the transition
// without reloading the aggregate.
public sealed record ApplicationStageChangedEvent(
    Guid ApplicationId,
    Guid FromStageId,
    Guid ToStageId,
    Guid TenantId) : INotification;
