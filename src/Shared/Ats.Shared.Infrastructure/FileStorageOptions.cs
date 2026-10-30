namespace Ats.Shared.Infrastructure;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    // Host:port only; UseSsl decides the scheme.
    public string Endpoint { get; init; } = "localhost:9000";

    // Secret: User Secrets or environment variables (see .env.example).
    public string AccessKey { get; init; } = null!;
    public string SecretKey { get; init; } = null!;

    public string BucketName { get; init; } = "cv-uploads";
    public bool UseSsl { get; init; }

    // Where the browser reaches MinIO, if not at Endpoint (e.g. the API runs in Docker). Download
    // links are signed for this host.
    public string? PublicEndpoint { get; init; }
    public bool PublicUseSsl { get; init; }

    public int MaxFileSizeBytes { get; init; } = 10 * 1024 * 1024;
}
