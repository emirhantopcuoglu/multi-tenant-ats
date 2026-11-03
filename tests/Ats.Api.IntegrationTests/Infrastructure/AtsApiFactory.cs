using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Ats.Api;
using Ats.Api.Extensions;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.Tenants.Application;
using Ats.Modules.Tenants.Domain;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.Minio;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Testcontainers.Redis;

namespace Ats.Api.IntegrationTests.Infrastructure;

// Real Program.cs on throwaway containers; RabbitMQ is replaced by the in-memory harness.
public sealed class AtsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Same image as docker-compose.
    private const string MinioImage = "cgr.dev/chainguard/minio:latest";
    private const string JwtSecret = "integration-tests-only-jwt-secret-0123456789";
    private const string CertificatePassword = "integration-tests";

    public const string SpaOrigin = "https://spa.test";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:7").Build();
    private readonly RedisContainer _redis = new RedisBuilder("redis:7").Build();
    private readonly MinioContainer _minio = new MinioBuilder(MinioImage).Build();
    private readonly string _certificatePath =
        Path.Combine(Path.GetTempPath(), $"ats-dataprotection-{Guid.NewGuid():N}.pfx");

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _mongo.StartAsync(), _redis.StartAsync(), _minio.StartAsync());
        await MigrateDatabaseAsync();
        WriteDataProtectionCertificate();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _mongo.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask(),
            _minio.DisposeAsync().AsTask());
        File.Delete(_certificatePath);
    }

    // The user isn't in the database; policies only read the token.
    public HttpClient CreateCompanyClient(string role, Guid tenantId)
    {
        using var scope = Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = $"{role.ToLowerInvariant()}@acme.test", TenantId = tenantId };

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", tokenService.GenerateAccessToken(user, [role]));
        return client;
    }

    // The account isn't in the database, so CandidateOnly endpoints fail its stamp check; company ones fail on role.
    public HttpClient CreateCandidateClient()
    {
        using var scope = Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ICandidateTokenService>();

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", tokenService.GenerateAccessToken(Guid.NewGuid(), "candidate@acme.test", Guid.NewGuid()));
        return client;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Keeps User Secrets and appsettings.Development.json out.
        builder.UseEnvironment("Testing");

        // Program.cs reads some settings before Build(), hence UseSetting.
        foreach (var (key, value) in BuildSettings())
            builder.UseSetting(key, value);

        builder.ConfigureTestServices(services =>
        {
            services.AddMassTransitTestHarness();
            services.AddTransient<IStartupFilter, ExternalClientIpStartupFilter>();
        });
    }

    private Dictionary<string, string> BuildSettings() => new()
    {
        ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
        ["Mongo:ConnectionString"] = _mongo.GetConnectionString(),
        ["Redis:ConnectionString"] = _redis.GetConnectionString(),
        ["FileStorage:Endpoint"] = $"{_minio.Hostname}:{_minio.GetMappedPublicPort(MinioBuilder.MinioPort)}",
        ["FileStorage:AccessKey"] = _minio.GetAccessKey(),
        ["FileStorage:SecretKey"] = _minio.GetSecretKey(),
        ["Jwt:Secret"] = JwtSecret,
        ["DataProtection:CertificatePath"] = _certificatePath,
        ["DataProtection:CertificatePassword"] = CertificatePassword,
        ["Cors:AllowedOrigins:0"] = SpaOrigin
    };

    // Startup seeds roles, so migrate first.
    private async Task MigrateDatabaseAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:Postgres"] = _postgres.GetConnectionString();
        builder.AddPersistence();

        using var host = builder.Build();
        await DatabaseMigrator.MigrateAsync(host.Services);
    }

    private void WriteDataProtectionCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Ats integration tests", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));

        File.WriteAllBytes(_certificatePath, certificate.Export(X509ContentType.Pfx, CertificatePassword));
    }
}
