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

// Pins the acting identity for services that read "who is calling" from the request scope.
internal sealed class FixedCurrentUser : ICurrentUser
{
    public FixedCurrentUser(Guid? userId, string? email = null)
    {
        UserId = userId;
        Email = email;
    }

    public Guid? UserId { get; }
    public string? Email { get; }
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

// Records keys instead of talking to MinIO. Tests must never reach real object storage, and which
// keys were written, copied and deleted is exactly what the CV flows need to be observable — an
// orphaned or prematurely deleted object is invisible in the database alone.
internal sealed class RecordingFileStorage : IFileStorage
{
    public List<string> Uploaded { get; } = [];
    public List<(string Source, string Destination)> Copied { get; } = [];
    public List<string> Deleted { get; } = [];

    public Task UploadAsync(
        string key, Stream content, long size, string contentType,
        CancellationToken cancellationToken = default)
    {
        Uploaded.Add(key);
        return Task.CompletedTask;
    }

    public Task<string> GetPresignedDownloadUrlAsync(
        string key, TimeSpan expiry, CancellationToken cancellationToken = default) =>
        Task.FromResult($"https://storage.test/{key}");

    public Task<byte[]> DownloadAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(Array.Empty<byte>());

    public Task CopyAsync(
        string sourceKey, string destinationKey, CancellationToken cancellationToken = default)
    {
        Copied.Add((sourceKey, destinationKey));
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        Deleted.Add(key);
        return Task.CompletedTask;
    }
}
