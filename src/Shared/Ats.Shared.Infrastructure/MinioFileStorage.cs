using Ats.Shared.Kernel;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Ats.Shared.Infrastructure;

// MinIO speaks the S3 API, so switching to AWS S3 is a config change.
public sealed class MinioFileStorage : IFileStorage
{
    // MinIO's default. Set on the presign client so signing never asks the server for the region,
    // which it couldn't reach at the public host.
    private const string Region = "us-east-1";

    private readonly IMinioClient _client;
    private readonly IMinioClient _presignClient;
    private readonly FileStorageOptions _options;

    public MinioFileStorage(IMinioClient client, IOptions<FileStorageOptions> options)
    {
        _client = client;
        _options = options.Value;
        _presignClient = string.IsNullOrWhiteSpace(_options.PublicEndpoint)
            ? client
            : CreatePresignClient(_options);
    }

    private static IMinioClient CreatePresignClient(FileStorageOptions options) =>
        new MinioClient()
            .WithEndpoint(options.PublicEndpoint)
            .WithCredentials(options.AccessKey, options.SecretKey)
            .WithSSL(options.PublicUseSsl)
            .WithRegion(Region)
            .Build();

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

        // Presigning happens locally, so there is no CancellationToken to pass.
        return await _presignClient.PresignedGetObjectAsync(args);
    }

    public async Task<byte[]> DownloadAsync(string key, CancellationToken cancellationToken = default)
    {
        // MinIO streams to a callback, so the object is buffered. CVs are capped at 10 MB.
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
