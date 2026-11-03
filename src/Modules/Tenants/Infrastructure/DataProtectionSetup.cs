using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Ats.Modules.Tenants.Infrastructure;

// The key ring signs Identity's email-confirmation and password-reset tokens. In Postgres it survives
// redeploys and every instance shares it; the certificate keeps a database dump from being enough to
// forge those tokens.
public static class DataProtectionSetup
{
    // Part of every key's purpose, so all instances must use the same name.
    public const string ApplicationName = "Ats";

    public static IDataProtectionBuilder AddAtsDataProtection(
        this IServiceCollection services, X509Certificate2 certificate) =>
        services.AddDataProtection()
            .SetApplicationName(ApplicationName)
            .PersistKeysToDbContext<TenantsDbContext>()
            .ProtectKeysWithCertificate(certificate);
}
