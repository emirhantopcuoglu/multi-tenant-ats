using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Ats.Api;
using Ats.Api.Extensions;
using Ats.Modules.CandidateAccounts.Application;
using Ats.Modules.CandidateAccounts.Domain;
using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Modules.Tenants.Application;
using Ats.Modules.Tenants.Domain;
using Ats.Shared.Kernel;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
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

    // A real account, so the token's stamp matches and only policies decide.
    public async Task<(HttpClient Client, Guid AccountId)> CreateCandidateClientAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CandidateAccountsDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<ICandidatePasswordHasher>();
        var account = CandidateAccount.Register(
            $"candidate-{Guid.NewGuid():N}@acme.test", hasher.Hash($"Aa1!{Guid.NewGuid():N}"),
            "Jane", "Doe", SupportedLanguages.Default);
        db.CandidateAccounts.Add(account);
        await db.SaveChangesAsync();

        var tokenService = scope.ServiceProvider.GetRequiredService<ICandidateTokenService>();
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", tokenService.GenerateAccessToken(account.Id, account.Email, account.SecurityStamp));
        return (client, account.Id);
    }

    // What a password change does to the account: tokens issued before it carry the old stamp.
    public async Task ChangeCandidatePasswordAsync(Guid accountId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CandidateAccountsDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<ICandidatePasswordHasher>();
        var account = await db.CandidateAccounts.SingleAsync(a => a.Id == accountId);
        account.ChangePassword(hasher.Hash($"Bb2@{Guid.NewGuid():N}"));
        await db.SaveChangesAsync();
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
        // Placeholders: the test harness replaces RabbitMQ and no test here checks mail.
        ["RabbitMq:Host"] = "localhost",
        ["RabbitMq:Username"] = "ats",
        ["RabbitMq:Password"] = "unused",
        ["Email:Host"] = "localhost",
        ["Email:Port"] = "1025",
        ["Email:FromAddress"] = "noreply@acme.test",
        ["FileStorage:Endpoint"] = $"{_minio.Hostname}:{_minio.GetMappedPublicPort(MinioBuilder.MinioPort)}",
        ["FileStorage:AccessKey"] = _minio.GetAccessKey(),
        ["FileStorage:SecretKey"] = _minio.GetSecretKey(),
        ["Jwt:Secret"] = JwtSecret,
        ["DataProtection:CertificatePath"] = _certificatePath,
        ["DataProtection:CertificatePassword"] = CertificatePassword,
        ["Cors:AllowedOrigins:0"] = SpaOrigin,
        ["Invitation:AcceptBaseUrl"] = $"{SpaOrigin}/accept-invitation",
        ["PasswordReset:ResetBaseUrl"] = $"{SpaOrigin}/reset-password",
        ["EmailConfirmation:ConfirmBaseUrl"] = $"{SpaOrigin}/confirm-email",
        ["CandidateEmailChange:ConfirmBaseUrl"] = $"{SpaOrigin}/candidate/confirm-email",
        ["CandidatePasswordReset:ResetBaseUrl"] = $"{SpaOrigin}/candidate/reset-password",
        ["CandidateEmailVerification:ConfirmBaseUrl"] = $"{SpaOrigin}/candidate/verify-email",
        ["InterviewRoom:BaseUrl"] = $"{SpaOrigin}/interview-room"
    };

    // Startup seeds roles, so migrate first.
    private async Task MigrateDatabaseAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration["ConnectionStrings:Postgres"] = _postgres.GetConnectionString();
        // The audit interceptor needs a Mongo client, even though migrations write no audit entries.
        builder.Configuration["Mongo:ConnectionString"] = _mongo.GetConnectionString();
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
