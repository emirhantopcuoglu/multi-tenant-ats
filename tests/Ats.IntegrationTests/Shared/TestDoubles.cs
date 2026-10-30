using Ats.Shared.Kernel;
using MediatR;
using Microsoft.Extensions.Logging;

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

// Records sent mail instead of using SMTP.
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

// Captures MediatR notifications so tests can check event payloads without a bus.
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

// Records written, copied and deleted keys instead of using MinIO.
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

// Keeps log entries so tests can check paths that succeed with a warning.
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));

    public bool Warned(string containing) =>
        Entries.Any(e =>
            e.Level == LogLevel.Warning
            && e.Message.Contains(containing, StringComparison.OrdinalIgnoreCase));
}
