using MediatR;

namespace Ats.Modules.Applications.Application.Events;

// No candidate contact details: nothing downstream sends an email for this.
public sealed record ApplicationWithdrawnEvent(
    Guid ApplicationId,
    Guid TenantId) : INotification;
