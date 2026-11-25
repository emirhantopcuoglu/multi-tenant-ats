using Ats.Modules.Applications.Infrastructure;
using Ats.Shared.Infrastructure;
using Microsoft.Extensions.Options;
using Minio;
using MongoDB.Driver;

namespace Ats.Api;

// Creates the MinIO bucket and the Mongo indexes in the background, so the API starts while either is
// down and finishes the setup once it is back.
public sealed class StorageInitializer : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(15);

    private readonly IMinioClient _minio;
    private readonly IOptions<FileStorageOptions> _fileStorageOptions;
    private readonly IMongoDatabase _mongo;
    private readonly ILogger<StorageInitializer> _logger;

    public StorageInitializer(
        IMinioClient minio, IOptions<FileStorageOptions> fileStorageOptions, IMongoDatabase mongo,
        ILogger<StorageInitializer> logger)
    {
        _minio = minio;
        _fileStorageOptions = fileStorageOptions;
        _mongo = mongo;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(
            RetryUntilDoneAsync(
                "MinIO bucket", ct => FileStorageInitializer.EnsureBucketAsync(_minio, _fileStorageOptions, ct),
                stoppingToken),
            RetryUntilDoneAsync(
                "Mongo indexes", ct => MongoActivityLogInitializer.EnsureIndexesAsync(_mongo, ct), stoppingToken));

    private async Task RetryUntilDoneAsync(
        string step, Func<CancellationToken, Task> run, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await run(stoppingToken);
                _logger.LogInformation("{Step} ready", step);
                return;
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(exception, "{Step} setup failed, retrying in {Delay}", step, RetryDelay);
            }

            try
            {
                await Task.Delay(RetryDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
