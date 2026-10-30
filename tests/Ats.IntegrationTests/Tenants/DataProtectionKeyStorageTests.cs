using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Ats.IntegrationTests.Shared;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Kernel;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Ats.IntegrationTests.Tenants;

[Collection("Integration")]
public sealed class DataProtectionKeyStorageTests
{
    private const string Purpose = "data-protection-key-storage-tests";

    private readonly PostgresContainerFixture _fixture;

    public DataProtectionKeyStorageTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task a_token_protected_before_a_restart_should_still_be_readable_after_it()
    {
        // Arrange
        using var certificate = CreateCertificate();
        string token;
        await using (var beforeRestart = BuildProvider(certificate))
        {
            token = beforeRestart.GetDataProtector(Purpose).Protect("payload");
        }

        // Act: a new container gets a new service provider and nothing in memory
        await using var afterRestart = BuildProvider(certificate);
        var payload = afterRestart.GetDataProtector(Purpose).Unprotect(token);

        // Assert
        Assert.Equal("payload", payload);
    }

    [Fact]
    public async Task keys_should_be_stored_in_postgres_encrypted_with_the_certificate()
    {
        using var certificate = CreateCertificate();
        await using var provider = BuildProvider(certificate);

        provider.GetDataProtector(Purpose).Protect("payload");

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TenantsDbContext>();
        var keys = await db.DataProtectionKeys.AsNoTracking().ToListAsync();

        Assert.NotEmpty(keys);
        // An unencrypted key holds its secret in a <masterKey><value> element.
        Assert.All(keys, key => Assert.Contains("encryptedSecret", key.Xml));
        Assert.All(keys, key => Assert.DoesNotContain("<value>", key.Xml));
    }

    private ServiceProvider BuildProvider(X509Certificate2 certificate)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ICurrentTenant>(_ => new FixedTenant(null));
        services.AddDbContext<TenantsDbContext>(options => options.UseNpgsql(
            _fixture.ConnectionString,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "tenants")));
        services.AddAtsDataProtection(certificate);
        return services.BuildServiceProvider();
    }

    // Round-tripped through PFX bytes, the same way the API loads its certificate from a file.
    private static X509Certificate2 CreateCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Ats data protection test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var selfSigned = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        return X509CertificateLoader.LoadPkcs12(selfSigned.Export(X509ContentType.Pfx), password: null);
    }
}
