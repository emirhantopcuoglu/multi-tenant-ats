namespace Ats.Shared.Infrastructure;

// Username and Password go in User Secrets or environment variables.
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
