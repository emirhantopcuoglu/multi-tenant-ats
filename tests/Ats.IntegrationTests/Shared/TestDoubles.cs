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

// For suites that construct a service which happens to depend on IEmailSender but never exercises a
// path that mails. Tests that assert on mail use RecordingEmailSender instead.
internal sealed class NoOpEmailSender : IEmailSender
{
    public Task SendAsync(
        string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

// In-memory IEmailSender: tests must never talk to a real SMTP server, and every flow that mails only
// needs "was a message handed to the port, to whom, saying what" to be observable. Shared because both
// the candidate and company recovery suites assert on the link they mail.
internal sealed class RecordingEmailSender : IEmailSender
{
    public List<(string ToEmail, string Subject, string Body)> Sent { get; } = [];

    public Task SendAsync(
        string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        Sent.Add((toEmail, subject, htmlBody));
        return Task.CompletedTask;
    }
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
