using Ats.Shared.Kernel;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Ats.Shared.Infrastructure;

// MinIO-backed IFileStorage. MinIO speaks the S3 API, so swapping it for AWS S3 in
// production is a configuration change, not a code change.
public sealed class MinioFileStorage : IFileStorage
{
    private readonly IMinioClient _client;
    private readonly FileStorageOptions _options;

    public MinioFileStorage(IMinioClient client, IOptions<FileStorageOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task UploadAsync(
        string key, Stream content, long size, string contentType,
        CancellationToken cancellationToken = default)
    {
        var args = new PutObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(key)
            .WithStreamData(content)
            .WithObjectSize(size)
            .WithContentType(contentType);

        await _client.PutObjectAsync(args, cancellationToken);
    }

    public async Task<string> GetPresignedDownloadUrlAsync(
        string key, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        var args = new PresignedGetObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(key)
            .WithExpiry((int)expiry.TotalSeconds);

        // The MinIO presign call builds the URL locally (no network round-trip), so it
        // exposes no CancellationToken to honour here.
        return await _client.PresignedGetObjectAsync(args);
    }

    public async Task<byte[]> DownloadAsync(string key, CancellationToken cancellationToken = default)
    {
        // MinIO streams the object to a callback rather than returning a stream, so we copy it into
        // a MemoryStream and hand back the bytes. CVs are small (10 MB cap at upload), so buffering
        // the whole object is acceptable here.
        using var buffer = new MemoryStream();
        var args = new GetObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(key)
            .WithCallbackStream((stream, ct) => stream.CopyToAsync(buffer, ct));

        await _client.GetObjectAsync(args, cancellationToken);
        return buffer.ToArray();
    }

    public async Task CopyAsync(
        string sourceKey, string destinationKey, CancellationToken cancellationToken = default)
    {
        var source = new CopySourceObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(sourceKey);

        var args = new CopyObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(destinationKey)
            .WithCopyObjectSource(source);

        await _client.CopyObjectAsync(args, cancellationToken);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var args = new RemoveObjectArgs()
            .WithBucket(_options.BucketName)
            .WithObject(key);

        await _client.RemoveObjectAsync(args, cancellationToken);
    }
}
