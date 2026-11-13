namespace Ats.Shared.Kernel;

// Implemented by every module that keeps data about an application. Called when the application's
// retention period ends. Must be safe to run again: a failed run starts over the next night.
public interface IApplicationDataEraser
{
    Task EraseAsync(IReadOnlyCollection<Guid> applicationIds, CancellationToken cancellationToken = default);
}
