using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Ats.Shared.Infrastructure;

// Ensures the target bucket exists before the app serves traffic. Idempotent — safe to run
// on every startup — mirroring how RoleSeeder guarantees the identity roles are present.
public static class FileStorageInitializer
{
    public static async Task EnsureBucketAsync(
        IMinioClient client, IOptions<FileStorageOptions> options,
        CancellationToken cancellationToken = default)
    {
        var bucket = options.Value.BucketName;

        var exists = await client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(bucket), cancellationToken);

        if (!exists)
            await client.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), cancellationToken);
    }
}
