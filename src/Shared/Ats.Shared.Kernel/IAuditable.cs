namespace Ats.Shared.Kernel;

// Filled by an EF interceptor, never by application code.
public interface IAuditable
{
    DateTime CreatedAtUtc { get; }
    Guid? CreatedBy { get; }
    DateTime? ModifiedAtUtc { get; }
    Guid? ModifiedBy { get; }
}
