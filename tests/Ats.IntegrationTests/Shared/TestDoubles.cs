using Ats.Shared.Kernel;
using MediatR;

namespace Ats.IntegrationTests.Shared;

internal sealed class FixedTenant : ICurrentTenant
{
    public FixedTenant(Guid? tenantId) => TenantId = tenantId;
    public Guid? TenantId { get; }
}

internal sealed class NullCurrentUser : ICurrentUser
{
    public Guid? UserId => null;
    public string? Email => null;
}

// Captures MediatR notifications published by command handlers so tests can assert on the exact
// event payload without a bus. The bridge handlers (in-process event -> integration event) are a
// pure mapping, so asserting the in-process event is the meaningful check.
internal sealed class CapturingPublisher : IPublisher
{
    public List<object> Published { get; } = [];

    public Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        Published.Add(notification);
        return Task.CompletedTask;
    }

    public Task Publish<TNotification>(
        TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        Published.Add(notification);
        return Task.CompletedTask;
    }
}
