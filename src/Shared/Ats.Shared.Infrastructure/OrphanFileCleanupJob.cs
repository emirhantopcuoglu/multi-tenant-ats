using Ats.Shared.Kernel;
using Microsoft.Extensions.Logging;

namespace Ats.Shared.Infrastructure;

// Deletes stored files no record points to, such as a CV whose application failed to save or whose
// delete failed after the record let it go. Scheduled by Hangfire.
public sealed class OrphanFileCleanupJob
{
    // An upload is saved within seconds; a day is a wide margin before calling it an orphan.
    public static readonly TimeSpan MinimumAge = TimeSpan.FromHours(24);

    // Keys asked about in one query, so a large bucket never becomes one huge IN list.
    private const int BatchSize = 500;

    private readonly IFileStorage _fileStorage;
    private readonly IReadOnlyList<IStoredFileOwner> _owners;
    private readonly ILogger<OrphanFileCleanupJob> _logger;

    public OrphanFileCleanupJob(
        IFileStorage fileStorage, IEnumerable<IStoredFileOwner> owners, ILogger<OrphanFileCleanupJob> logger)
    {
        _fileStorage = fileStorage;
        _owners = owners.ToList();
        _logger = logger;
    }

    public async Task CleanupAsync(CancellationToken ct = default)
    {
        // Without an owner every file would look unused.
        if (_owners.Count == 0)
            throw new InvalidOperationException("No IStoredFileOwner is registered; refusing to delete files.");

        var cutoff = DateTime.UtcNow - MinimumAge;
        var batch = new List<string>(BatchSize);
        var deletedCount = 0;

        await foreach (var file in _fileStorage.ListAsync(ct))
        {
            if (file.LastModifiedUtc > cutoff)
                continue;

            batch.Add(file.Key);
            if (batch.Count == BatchSize)
            {
                deletedCount += await DeleteUnreferencedAsync(batch, ct);
                batch.Clear();
            }
        }

        if (batch.Count > 0)
            deletedCount += await DeleteUnreferencedAsync(batch, ct);

        _logger.LogInformation("Orphan file cleanup removed {DeletedCount} file(s)", deletedCount);
    }

    // Every owner answers before anything is deleted; if one throws, nothing in the batch goes.
    private async Task<int> DeleteUnreferencedAsync(List<string> keys, CancellationToken ct)
    {
        var referenced = new HashSet<string>();
        foreach (var owner in _owners)
            referenced.UnionWith(await owner.FindReferencedAsync(keys, ct));

        var orphans = keys.Where(key => !referenced.Contains(key)).ToList();
        foreach (var key in orphans)
        {
            await _fileStorage.DeleteAsync(key, ct);
            _logger.LogInformation("Deleted orphan file {FileKey}", key);
        }

        return orphans.Count;
    }
}
