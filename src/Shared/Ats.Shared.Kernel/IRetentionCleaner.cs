namespace Ats.Shared.Kernel;

// Implemented by every module that keeps records past their use, such as expired tokens or old audit
// rows. The retention cleanup job calls them all every night with the same cutoffs.
public interface IRetentionCleaner
{
    // Returns the number of rows deleted.
    Task<int> CleanAsync(RetentionCutoffs cutoffs, CancellationToken cancellationToken = default);
}
