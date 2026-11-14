using Ats.Modules.CandidateAccounts.Infrastructure;
using Ats.Modules.Notifications.Infrastructure;
using Ats.Modules.Tenants.Infrastructure;
using Ats.Shared.Infrastructure;

namespace Ats.Api.Extensions;

// Settings with no safe default. appsettings.json leaves them out on purpose, so a deployment that
// forgets one stops here with the whole list instead of starting on an empty or dev value.
public static class RequiredSettings
{
    private static readonly string[] Values =
    [
        "ConnectionStrings:Postgres",
        Key(MongoOptions.SectionName, nameof(MongoOptions.ConnectionString)),
        Key(RedisOptions.SectionName, nameof(RedisOptions.ConnectionString)),
        Key(RabbitMqOptions.SectionName, nameof(RabbitMqOptions.Host)),
        Key(RabbitMqOptions.SectionName, nameof(RabbitMqOptions.Username)),
        Key(RabbitMqOptions.SectionName, nameof(RabbitMqOptions.Password)),
        Key(JwtOptions.SectionName, nameof(JwtOptions.Secret)),
        Key(DataProtectionExtensions.SectionName, DataProtectionExtensions.CertificatePathKey),
        Key(DataProtectionExtensions.SectionName, DataProtectionExtensions.CertificatePasswordKey),
        Key(FileStorageOptions.SectionName, nameof(FileStorageOptions.Endpoint)),
        Key(FileStorageOptions.SectionName, nameof(FileStorageOptions.AccessKey)),
        Key(FileStorageOptions.SectionName, nameof(FileStorageOptions.SecretKey)),
        Key(EmailOptions.SectionName, nameof(EmailOptions.Host)),
        Key(EmailOptions.SectionName, nameof(EmailOptions.Port)),
        Key(EmailOptions.SectionName, nameof(EmailOptions.FromAddress)),
        // The SPA is served from its own origin.
        Key(CorsOptions.SectionName, nameof(CorsOptions.AllowedOrigins), "0")
    ];

    // Mailed links: a wrong one only shows up in someone's inbox.
    private static readonly string[] AbsoluteUrls =
    [
        Key(InvitationOptions.SectionName, nameof(InvitationOptions.AcceptBaseUrl)),
        Key(PasswordResetOptions.SectionName, nameof(PasswordResetOptions.ResetBaseUrl)),
        Key(EmailConfirmationOptions.SectionName, nameof(EmailConfirmationOptions.ConfirmBaseUrl)),
        Key(CandidateEmailChangeOptions.SectionName, nameof(CandidateEmailChangeOptions.ConfirmBaseUrl)),
        Key(CandidatePasswordResetOptions.SectionName, nameof(CandidatePasswordResetOptions.ResetBaseUrl)),
        Key(CandidateEmailVerificationOptions.SectionName, nameof(CandidateEmailVerificationOptions.ConfirmBaseUrl)),
        Key(InterviewRoomOptions.SectionName, nameof(InterviewRoomOptions.BaseUrl))
    ];

    public static void EnsureRequiredSettings(this IConfiguration configuration)
    {
        var missing = Values
            .Where(key => string.IsNullOrWhiteSpace(configuration[key]))
            .Select(key => $"{key} is missing");
        var notUrls = AbsoluteUrls
            .Where(key => !IsHttpUrl(configuration[key]))
            .Select(key => $"{key} is not an absolute http(s) URL");

        var problems = missing.Concat(notUrls).ToList();
        if (problems.Count > 0)
            throw new InvalidOperationException($"Settings are not configured: {string.Join("; ", problems)}.");
    }

    // The scheme check matters on Linux, where "/interview-room" parses as an absolute file:// URI.
    private static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string Key(params string[] parts) => string.Join(':', parts);
}
