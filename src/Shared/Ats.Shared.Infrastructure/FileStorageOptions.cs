namespace Ats.Shared.Infrastructure;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    // Host:port of the storage API (MinIO defaults to :9000). No scheme — UseSsl decides that.
    public string Endpoint { get; init; } = "localhost:9000";

    // Credentials are secrets: they live in User Secrets / environment variables, never in
    // appsettings.json. See .env.example for the variable names.
    public string AccessKey { get; init; } = null!;
    public string SecretKey { get; init; } = null!;

    public string BucketName { get; init; } = "cv-uploads";
    public bool UseSsl { get; init; }

    // Upload ceiling enforced at the boundary before anything touches storage. Defaults to
    // 10 MB; overridable from config so ops can tune it without a code change.
    public int MaxFileSizeBytes { get; init; } = 10 * 1024 * 1024;
}
