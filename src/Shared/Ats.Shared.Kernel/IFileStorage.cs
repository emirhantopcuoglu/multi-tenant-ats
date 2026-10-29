namespace Ats.Shared.Kernel;

// The bucket comes from configuration; callers choose the keys. Application CVs are keyed per
// tenant, account CVs aren't ("candidates/{accountId}/...").
public interface IFileStorage
{
    // Size is passed in because not every stream is seekable and the upload needs it up front.
    Task UploadAsync(
        string key, Stream content, long size, string contentType,
        CancellationToken cancellationToken = default);

    // The client downloads straight from storage; the bucket stays private.
    Task<string> GetPresignedDownloadUrlAsync(
        string key, TimeSpan expiry, CancellationToken cancellationToken = default);

    // Whole object in memory, for server-side processing. Fine because CVs are capped at upload.
    Task<byte[]> DownloadAsync(string key, CancellationToken cancellationToken = default);

    // Server-side copy, so the bytes never pass through the API. Used so an application keeps its own
    // copy of the CV even if the account's CV changes or is deleted.
    Task CopyAsync(string sourceKey, string destinationKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
