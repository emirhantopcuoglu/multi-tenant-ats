using System.Security.Cryptography.X509Certificates;
using Ats.Modules.Tenants.Infrastructure;

namespace Ats.Api.Extensions;

public static class DataProtectionExtensions
{
    private const string SectionName = "DataProtection";

    public static IHostApplicationBuilder AddDataProtectionKeys(this IHostApplicationBuilder builder)
    {
        var options = builder.Configuration.GetSection(SectionName).Get<CertificateOptions>() ?? new CertificateOptions();
        var certificate = LoadCertificate(options, builder.Environment);

        builder.Services.AddAtsDataProtection(certificate);
        return builder;
    }

    // Required in every environment: a key written with the certificate can't be read without it, and
    // dev and Docker share the same database.
    private static X509Certificate2 LoadCertificate(CertificateOptions options, IHostEnvironment environment)
    {
        if (string.IsNullOrWhiteSpace(options.CertificatePath))
        {
            throw new InvalidOperationException(
                "DataProtection:CertificatePath is not set. Create one with scripts/new-dataprotection-cert.sh.");
        }

        var path = Path.Combine(environment.ContentRootPath, options.CertificatePath);
        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"DataProtection certificate not found at {path}.");
        }

        var certificate = X509CertificateLoader.LoadPkcs12FromFile(path, options.CertificatePassword);

        return certificate.HasPrivateKey
            ? certificate
            : throw new InvalidOperationException("The DataProtection certificate has no private key.");
    }

    private sealed class CertificateOptions
    {
        public string? CertificatePath { get; init; }
        public string? CertificatePassword { get; init; }
    }
}
