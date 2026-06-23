namespace Ats.Shared.Kernel;

// A port for making a side effect run at most once for a given key, even when the same message is
// delivered more than once. RabbitMQ/MassTransit guarantee at-least-once delivery: a message can be
// redelivered after a lost ack, after a retry, or when a faulted message is replayed from the error
// queue. For a side effect like sending an email that is not naturally idempotent, this guard keeps a
// duplicate delivery from producing a duplicate effect.
//
// Like IFileStorage and IEmailSender, this is a cross-cutting infrastructure capability, so the
// abstraction lives in the kernel and the (Redis-backed) implementation lives in shared infrastructure.
public interface IIdempotencyGuard
{
    // Runs `operation` only the first time `key` is seen. The claim is taken atomically before the
    // operation runs and kept on success, so a later delivery of the same message is skipped. If the
    // operation throws, the claim is released so the message can be retried (or, after all retries are
    // exhausted, dead-lettered and replayed later). Returns true if the operation ran, false if it was
    // skipped as a duplicate.
    Task<bool> ProcessOnceAsync(string key, Func<Task> operation);
}
