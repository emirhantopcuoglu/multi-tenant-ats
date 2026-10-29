namespace Ats.Shared.Contracts.Applications;

// Published when a candidate withdraws. Only the Interviews module consumes it, to free the booked
// slots, so it carries no contact details.
public sealed record ApplicationWithdrawnIntegrationEvent(
    Guid ApplicationId,
    Guid TenantId);
