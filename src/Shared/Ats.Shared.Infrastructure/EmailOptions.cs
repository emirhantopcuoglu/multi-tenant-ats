namespace Ats.Shared.Infrastructure;

// SMTP settings for the shared email sender. Bound from the "Email" configuration section in
// Program.cs. Dev points at MailHog (no auth); a real SMTP server sets Username/Password, which
// belong in User Secrets / environment variables rather than appsettings.json.
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 1025;
    public string FromAddress { get; init; } = "noreply@ats.local";
    public string FromName { get; init; } = "ATS";
    public bool UseSsl { get; init; } = false;
    public string? Username { get; init; }
    public string? Password { get; init; }
}
