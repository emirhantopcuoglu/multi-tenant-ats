namespace Ats.Shared.Kernel;

// Implemented by each module that keeps file keys, so the orphan cleanup can ask without knowing
// the modules.
public interface IStoredFileOwner
{
    // The subset of keys this module still points to.
    Task<IReadOnlyCollection<string>> FindReferencedAsync(
        IReadOnlyCollection<string> keys, CancellationToken cancellationToken = default);
}
