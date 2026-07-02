using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Ats.Shared.Infrastructure;

public sealed class MinioHealthCheck : IHealthCheck
{
    private readonly IMinioClient _client;
    private readonly string _bucketName;

    public MinioHealthCheck(IMinioClient client, IOptions<FileStorageOptions> options)
    {
        _client = client;
        _bucketName = options.Value.BucketName;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var exists = await _client.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_bucketName), cancellationToken);

            return exists
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded($"Bucket '{_bucketName}' does not exist.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(exception: ex);
        }
    }
}
