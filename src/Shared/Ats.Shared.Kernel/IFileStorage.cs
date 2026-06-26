namespace Ats.Shared.Kernel;

// Abstraction over object storage (MinIO in dev, any S3-compatible backend in prod).
// Lives in the Kernel as behaviour so Application/Domain code can depend on it without
// referencing the concrete storage SDK — same pattern as ICurrentUser / ICurrentTenant.
//
// The bucket is a deployment concern owned by the implementation (configuration), not the
// caller: callers only ever deal in object keys. The key layout (for CVs:
// "{tenantId}/{applicationId}/{guid}-{originalName}") is chosen by the caller, since only it
// knows the tenant and application the file belongs to.
public interface IFileStorage
{
    // 'size' is passed explicitly rather than read from the stream: not every stream is
    // seekable, and S3-style uploads need the length up front to avoid buffering the whole
    // object in memory.
    Task UploadAsync(
        string key, Stream content, long size, string contentType,
        CancellationToken cancellationToken = default);

    // Returns a time-limited, signed URL that lets a client download the object directly from
    // storage — the file never streams through the API. The link stops working once 'expiry'
    // elapses, which is why the bucket itself stays private.
    Task<string> GetPresignedDownloadUrlAsync(
        string key, TimeSpan expiry, CancellationToken cancellationToken = default);

    // Reads the whole object into memory. Unlike GetPresignedDownloadUrlAsync (for the browser to
    // pull a file directly), this is for server-side processing that needs the bytes in-process —
    // e.g. the CV-parsing consumer extracting text. Returning a byte[] is fine because CVs are
    // capped at a few MB at upload; a streaming overload can be added if larger objects appear.
    Task<byte[]> DownloadAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
