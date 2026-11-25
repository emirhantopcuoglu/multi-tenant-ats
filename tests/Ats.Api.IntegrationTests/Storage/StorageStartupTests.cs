using System.Net;
using Ats.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Minio;
using Minio.DataModel.Args;

namespace Ats.Api.IntegrationTests.Storage;

[Collection(ApiTestCollection.Name)]
public sealed class StorageStartupTests
{
    private readonly AtsApiFactory _factory;

    public StorageStartupTests(AtsApiFactory factory) => _factory = factory;

    [Fact]
    public async Task the_api_should_start_while_minio_and_mongo_are_down()
    {
        await using var factory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("FileStorage:Endpoint", "localhost:1");
            builder.UseSetting("Mongo:ConnectionString", "mongodb://localhost:1/?serverSelectionTimeoutMS=500");
        });

        using var response = await factory.CreateClient().GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task the_bucket_should_be_created_once_the_api_is_up()
    {
        var bucket = $"fresh-{Guid.NewGuid():N}"[..20];
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("FileStorage:BucketName", bucket));
        var minio = factory.Services.GetRequiredService<IMinioClient>();

        var exists = false;
        for (var attempt = 0; attempt < 50 && !exists; attempt++)
        {
            exists = await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket));
            if (!exists)
                await Task.Delay(200);
        }

        Assert.True(exists);
    }
}
