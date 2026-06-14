namespace Ats.Shared.Kernel;

// Marks an entity whose creation and last modification are tracked automatically.
// Setters are intentionally absent: values are written by the persistence layer
// (an EF Core interceptor), never by application code.
public interface IAuditable
{
    DateTime CreatedAtUtc { get; }
    Guid? CreatedBy { get; }
    DateTime? ModifiedAtUtc { get; }
    Guid? ModifiedBy { get; }
}
