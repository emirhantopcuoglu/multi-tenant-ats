namespace Ats.Shared.Kernel;

// Deletes become a flag update, and a global query filter hides flagged rows.
public interface ISoftDeletable
{
    bool IsDeleted { get; }
    DateTime? DeletedAtUtc { get; }
    Guid? DeletedBy { get; }
}
