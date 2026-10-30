namespace Ats.Shared.Kernel;

// Runs a side effect at most once per key, since messages can be delivered more than once.
public interface IIdempotencyGuard
{
    // Claims the key before running and keeps it on success. If the operation throws, the claim is
    // released so a retry can run. Returns false for a duplicate.
    Task<bool> ProcessOnceAsync(string key, Func<Task> operation);
}
