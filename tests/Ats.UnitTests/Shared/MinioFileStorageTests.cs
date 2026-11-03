using Ats.Shared.Infrastructure;
using Microsoft.Extensions.Options;
using Minio;

namespace Ats.UnitTests.Shared;

// Presigning is computed locally, so these run without a MinIO server.
public class MinioFileStorageTests
{
    [Fact]
    public async Task Download_links_should_use_the_public_endpoint_when_set()
    {
        // Arrange
        var storage = CreateStorage(publicEndpoint: "localhost:9000");

        // Act
        var url = await storage.GetPresignedDownloadUrlAsync("cvs/a.pdf", TimeSpan.FromMinutes(5));

        // Assert
        Assert.StartsWith("http://localhost:9000/cv-uploads/cvs/a.pdf?", url);
    }

    [Fact]
    public async Task Download_links_should_use_the_endpoint_when_no_public_endpoint_is_set()
    {
        var storage = CreateStorage(publicEndpoint: null);

        var url = await storage.GetPresignedDownloadUrlAsync("cvs/a.pdf", TimeSpan.FromMinutes(5));

        Assert.StartsWith("http://minio:9000/cv-uploads/cvs/a.pdf?", url);
    }

    private static MinioFileStorage CreateStorage(string? publicEndpoint)
    {
        var options = new FileStorageOptions
        {
            Endpoint = "minio:9000",
            AccessKey = "access",
            SecretKey = "secret",
            PublicEndpoint = publicEndpoint,
        };

        // Region set so the internal client doesn't look it up over the network either.
        var client = new MinioClient()
            .WithEndpoint(options.Endpoint)
            .WithCredentials(options.AccessKey, options.SecretKey)
            .WithRegion("us-east-1")
            .Build();

        return new MinioFileStorage(client, Options.Create(options));
    }
}
